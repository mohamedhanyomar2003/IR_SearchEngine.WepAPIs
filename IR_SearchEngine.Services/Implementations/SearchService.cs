using IR_SearchEngine.Services.Implementations;
using IR_SearchEngine.Core.DTOs;
using IR_SearchEngine.Core.Enums;
using IR_SearchEngine.Core.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IR_SearchEngine.Services.Implementations
{
    public class SearchService : ISearchService
    {
        private readonly IDataRepository _repo;
        private readonly ITextProcessor _processor;

        public SearchService(IDataRepository repo, ITextProcessor processor)
        {
            _repo = repo;
            _processor = processor;
        }

        public SearchResponseDto Search(SearchRequestDto request)
        {
            var response = new SearchResponseDto();
            HashSet<int> resultIds = new HashSet<int>();
            response.ProcessingSteps.Add($"Search Type: {request.SearchType}");

            switch (request.SearchType)
            {
                case SearchType.Boolean:
                    resultIds = ExecuteBoolean(request.Query, response.ProcessingSteps);
                    break;
                case SearchType.Phrase:
                    resultIds = ExecutePhrase(request.Query, response.ProcessingSteps);
                    break;
                case SearchType.Soundex:
                    resultIds = ExecuteSoundex(request.Query, response.ProcessingSteps, response.SuggestedTerms);
                    break;
            }

            response.TotalResults = resultIds.Count;
            foreach (var id in resultIds)
                if (_repo.GetAllDocuments().TryGetValue(id, out var content))
                    response.Documents.Add(new DocumentResultDto { DocId = id, Content = content });

            return response;
        }

        // --- 1. Boolean (Shunting Yard) ---
        private HashSet<int> ExecuteBoolean(string query, List<string> steps)
        {
            string tuned = query.Replace("(", " ( ").Replace(")", " ) ");
            var rawtokens = tuned.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            // 👇👇 التعديل هنا: تنظيف الكويري قبل الشغل 👇👇
            var tokensList = SanitizeQueryTokens(rawtokens);

            // لو الكويري فضيت بعد التنظيف (كانت كلها AND AND OR) رجع فاضي
            if (tokensList.Count == 0) return new HashSet<int>();

            // حولناها لـ Array عشان نكمل الكود القديم
            var tokens = tokensList.ToArray();
            steps.Add($"1. Sanitized Query: {string.Join(" ", tokens)}");

            var postfix = InfixToPostfix(tokens);

            // تسجيل الخطوة: التحويل لـ Postfix
            steps.Add($"2. Postfix Expression: {string.Join(" ", postfix)}");

            var stack = new Stack<HashSet<int>>();
            var allDocs = new HashSet<int>(_repo.GetAllDocuments().Keys);

            foreach (var token in postfix)
            {
                if (token == "AND")
                {
                    var s2 = stack.Pop(); var s1 = stack.Pop();
                    s1.IntersectWith(s2);
                    stack.Push(s1);
                    steps.Add($"-> Operation: Intersected 2 sets. Result count: {s1.Count}");
                }
                else if (token == "OR")
                {
                    var s2 = stack.Pop(); var s1 = stack.Pop();
                    s1.UnionWith(s2);
                    stack.Push(s1);
                    steps.Add($"-> Operation: Union 2 sets. Result count: {s1.Count}");
                }
                else if (token == "NOT")
                {
                    var s = stack.Pop();
                    var res = new HashSet<int>(allDocs);
                    res.ExceptWith(s);
                    stack.Push(res);
                    steps.Add($"-> Operation: NOT set applied.");
                }
                else
                {
                    var term = _processor.ApplyStemming(token.ToLower());
                    var docs = _repo.GetInvertedIndex(term);
                    stack.Push(new HashSet<int>(docs)); // Important: Create copy
                    steps.Add($"-> Term Processed: '{token}' -> Stem: '{term}' -> Found in {docs.Count} docs");
                }
            }

            return stack.Count > 0 ? stack.Pop() : new HashSet<int>();
        }

        // --- 2. Phrase ---
        private HashSet<int> ExecutePhrase(string query, List<string> steps)
        {
            var terms = _processor.Analyze(query, out _);
            steps.Add($"1. Phrase Terms Analyzed: {string.Join(" -> ", terms)}");

            if (terms.Count == 0) return new HashSet<int>();

            var positionsList = terms.Select(t => _repo.GetPositionalIndex(t)).ToList();
            if (positionsList.Any(p => p == null))
            {
                steps.Add("-> One or more terms not found in index.");
                return new HashSet<int>();
            }

            var candidateDocs = new HashSet<int>(positionsList[0].Keys);
            for (int i = 1; i < positionsList.Count; i++) candidateDocs.IntersectWith(positionsList[i].Keys);

            steps.Add($"2. Initial Candidate Docs (Intersection): {string.Join(", ", candidateDocs)}");

            var finalDocs = new HashSet<int>();
            foreach (var doc in candidateDocs)
            {
                var current = positionsList[0][doc];
                for (int i = 1; i < positionsList.Count; i++)
                {
                    var next = positionsList[i][doc];
                    current = current.Select(p => p + 1).Intersect(next).ToList();
                    if (!current.Any()) break;
                }
                if (current.Any()) finalDocs.Add(doc);
            }

            steps.Add($"3. Final Matches (Sequence Check): {finalDocs.Count} docs");
            return finalDocs;
        }
        private HashSet<int> ExecuteSoundex(string query, List<string> steps, List<string> suggestions)
        {
            string rawTerm = query.Split(' ')[0];
            string code = GenerateSoundex(rawTerm);

            steps.Add($"1. Generated Soundex Code for '{rawTerm}': {code}");

            var result = new HashSet<int>();
            var matchingTerms = new List<string>();

            foreach (var key in _repo.GetInvertedIndexKeys())
            {
                if (GenerateSoundex(key) == code)
                {
                    result.UnionWith(_repo.GetInvertedIndex(key));
                    matchingTerms.Add(key);
                    if (!suggestions.Contains(key))
                    {
                        suggestions.Add(key); 
                    }
                }
            }

            steps.Add($"2. Terms matching code '{code}': {string.Join(", ", matchingTerms)}");
            return result;
        }
        private string GenerateSoundex(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.ToUpper();
            StringBuilder sb = new StringBuilder();
            sb.Append(s[0]);
            for (int i = 1; i < s.Length; i++)
            {
                char c = s[i], code = '0';
                if ("BFPV".Contains(c)) code = '1';
                else if ("CGJKQSXZ".Contains(c)) code = '2';
                else if ("DT".Contains(c)) code = '3';
                else if ("L".Contains(c)) code = '4';
                else if ("MN".Contains(c)) code = '5';
                else if ("R".Contains(c)) code = '6';
                if (code != '0' && code != sb[sb.Length - 1]) sb.Append(code);
            }
            string res = sb.ToString().Replace("0", "");
            return (res + "0000").Substring(0, 4);
        }

        private Queue<string> InfixToPostfix(string[] tokens)
        {
            var output = new Queue<string>();
            var ops = new Stack<string>();
            var prec = new Dictionary<string, int> { { "OR", 1 }, { "AND", 2 }, { "NOT", 3 } };

            foreach (var t in tokens)
            {
                string up = t.ToUpper();
                if (prec.ContainsKey(up))
                {
                    while (ops.Count > 0 && ops.Peek() != "(" && prec.GetValueOrDefault(ops.Peek(), 0) >= prec[up])
                        output.Enqueue(ops.Pop());
                    ops.Push(up);
                }
                else if (t == "(") ops.Push("(");
                else if (t == ")") { while (ops.Count > 0 && ops.Peek() != "(") output.Enqueue(ops.Pop()); ops.Pop(); }
                else output.Enqueue(t);
            }
            while (ops.Count > 0) output.Enqueue(ops.Pop());
            return output;
        }
        private List<string> SanitizeQueryTokens(string[] tokens)
        {
            var cleanTokens = new List<string>();
            var operators = new HashSet<string> { "AND", "OR", "NOT" };
            var binaryOperators = new HashSet<string> { "AND", "OR" }; 

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i].ToUpper();
                string prevToken = cleanTokens.Count > 0 ? cleanTokens.Last().ToUpper() : "";

                // 1. لو التوكن الحالي Operator
                if (operators.Contains(token))
                {
                    // أ. لو جه في الأول خالص (ومش NOT) -> تجاهله
                    // مثال: "AND Ahmed" -> تبقى "Ahmed"
                    if (cleanTokens.Count == 0 && token != "NOT") continue;

                    // ب. لو قبله Operator تاني
                    if (operators.Contains(prevToken))
                    {
                        // حالة خاصة مسموحة: AND NOT أو OR NOT
                        if (token == "NOT" && binaryOperators.Contains(prevToken))
                        {
                            cleanTokens.Add(tokens[i]); // ضيف الـ NOT عادي
                        }
                        else
                        {
                            // غير كده، تجاهل الـ Operator الحالي (تكرار)
                            // مثال: "Ahmed AND OR Ali" -> هتتجاهل OR وتبقى "Ahmed AND Ali"
                            continue;
                        }
                    }
                    else
                    {
                        // لو قبله كلمة عادية أو قوس، ضيف الـ Operator عادي
                        cleanTokens.Add(tokens[i]);
                    }
                }
                else
                {
                    // لو كلمة عادية ضيفها
                    cleanTokens.Add(tokens[i]);
                }
            }

            // 2. تنظيف النهاية: لو آخر حاجة Operator شيلها
            // مثال: "Ahmed AND" -> تبقى "Ahmed"
            if (cleanTokens.Count > 0 && operators.Contains(cleanTokens.Last().ToUpper()))
            {
                cleanTokens.RemoveAt(cleanTokens.Count - 1);
            }

            return cleanTokens;
        }
    }
}