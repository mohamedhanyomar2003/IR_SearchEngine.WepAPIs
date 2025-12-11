using IR_SearchEngine.Core.Interfaces;
using System.Linq;

namespace IR_SearchEngine.Services.Implementations
{
    public class IndexingService : IIndexingService
    {
        private readonly IDataRepository _repo;
        private readonly ITextProcessor _processor;

        public IndexingService(IDataRepository repo, ITextProcessor processor)
        {
            _repo = repo;
            _processor = processor;
        }

        public void IndexAllDocuments()
        {
            foreach (var doc in _repo.GetAllDocuments()) IndexDocument(doc.Key, doc.Value);
        }

        public void IndexDocument(int id, string content)
        {
          
            _repo.AddDocument(id, content);

            var tokensWithPos = _processor.AnalyzeWithPositions(content);

            foreach (var item in tokensWithPos)
            {
                string term = item.term;     
                int position = item.position;

                // Inverted Index
                _repo.AddToInvertedIndex(term, id);

                // Positional Index
                _repo.AddToPositionalIndex(term, id, position);
            }
        }


        public Dictionary<int, string> GetAllDocuments()
        {
            return _repo.GetAllDocuments();
        }
    }
}