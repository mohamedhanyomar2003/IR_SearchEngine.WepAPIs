using IR_SearchEngine.Core.Interfaces;
using System.Collections.Generic;

namespace IR_SearchEngine.Infrastructure.Repositories
{
    public class DataRepository : IDataRepository
    {
        // Singleton Stores
        public Dictionary<int, string> Documents { get; private set; }
        public Dictionary<string, HashSet<int>> InvertedIndex { get; private set; }
        public Dictionary<string, Dictionary<int, List<int>>> PositionalIndex { get; private set; }

        public DataRepository()
        {
            Documents = new Dictionary<int, string>();
            InvertedIndex = new Dictionary<string, HashSet<int>>();
            PositionalIndex = new Dictionary<string, Dictionary<int, List<int>>>();
            SeedData();
        }

        private void SeedData()
        {
            // --- Category 1: Medical & Science (From IR Labs) ---
            AddDocument(1, "breakthrough drug for schizophrenia");
            AddDocument(2, "new schizophrenia drug");
            AddDocument(3, "new approach for treatment of schizophrenia");
            AddDocument(4, "new hopes for schizophrenia patients");
            AddDocument(5, "The study of relativity and theoretical physics");

            // --- Category 2: Cars & Business () ---
            AddDocument(6, "Chevrolet and Renault were winners");
            AddDocument(7, "Renault and Chevrolet were the number four seller");
            AddDocument(8, "Chevrolet produces vehicles");
            AddDocument(9, "Ford and Chevrolet are competing in the market");

            // --- Category 3: History & Literature ( Shakespeare) ---
            AddDocument(10, "When Antony found Julius Caesar dead");
            AddDocument(11, "When at Philippi he found Brutus slain");
            AddDocument(12, "I did enact Julius Caesar, Brutus killed me");
            AddDocument(13, "The noble Brutus hath told you Caesar was ambitious");

            // --- Category 4: Stemming Stress Test () ---
            // الغرض: التأكد ان connect, connected, connecting   
            AddDocument(14, "I want to connect to the internet");
            AddDocument(15, "We are connected by a strong network");
            AddDocument(16, "Connecting people is our mission");
            AddDocument(17, "The connection was lost suddenly");
            AddDocument(18, "Making connections is vital for business");

            // الغرض: تجربة قواعد Y و IES
            AddDocument(19, "The pony runs in the field");
            AddDocument(20, "Look at the ponies running together");
            AddDocument(21, "The sky is blue");
            AddDocument(22, "The skies are clear today");

            // --- Category 5: Soundex Test (Phonetic) ---
            // الغرض: تجربة البحث عن Smith ويطلعلك Smyth
            AddDocument(23, "Mr. Smith is a great engineer");
            AddDocument(24, "Mrs. Smyth works at the same company");
            AddDocument(25, "My name is Robert");
            AddDocument(26, "Rupert is my brother"); // Robert & Rupert often have similar codes

            // --- Category 6: Phrase Search Test ---
            AddDocument(27, "to be or not to be that is the question");
            AddDocument(28, "angels fear to tread where fools rush in");
            AddDocument(29, "fools rush in where angels fear to tread");
            AddDocument(30, "information retrieval systems are complex");
        }
        public Dictionary<int, string> GetAllDocuments() => Documents;

        public void AddDocument(int id, string content)
        {
            if (!Documents.ContainsKey(id)) Documents[id] = content;
        }

        public void AddToInvertedIndex(string term, int docId)
        {
            if (!InvertedIndex.ContainsKey(term)) InvertedIndex[term] = new HashSet<int>();
            InvertedIndex[term].Add(docId);
        }

        public HashSet<int> GetInvertedIndex(string term)
        {
            return InvertedIndex.ContainsKey(term) ? InvertedIndex[term] : new HashSet<int>();
        }

        public IEnumerable<string> GetInvertedIndexKeys() => InvertedIndex.Keys;

        public void AddToPositionalIndex(string term, int docId, int position)
        {
            if (!PositionalIndex.ContainsKey(term)) PositionalIndex[term] = new Dictionary<int, List<int>>();
            if (!PositionalIndex[term].ContainsKey(docId)) PositionalIndex[term][docId] = new List<int>();
            PositionalIndex[term][docId].Add(position);
        }

        public Dictionary<int, List<int>> GetPositionalIndex(string term)
        {
            return PositionalIndex.ContainsKey(term) ? PositionalIndex[term] : [];
        }
    }
}