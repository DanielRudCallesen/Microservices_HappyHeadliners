using System.Collections.Concurrent;
using NewsletterService.Model;

namespace NewsletterService.Services
{
    public class InMemoryImmediateArticleStore : IImmediateArticleStore
    {
        private readonly ConcurrentQueue<ImmediateArticle> _queue = new();

        public void Add(ImmediateArticle article)
        {
            _queue.Enqueue(article);
            // Bound Size
            while (_queue.Count > 500 && _queue.TryDequeue(out _)) {}
        }

        public IReadOnlyCollection<ImmediateArticle> GetLatest(int max = 50) => _queue.Reverse().Take(max).ToList();

        public void Prune(TimeSpan maxAge)
        {
            var cutoff = DateTimeOffset.UtcNow - maxAge;
            var items = _queue.ToArray();
            _queue.Clear();
            foreach ( var a in items.Where(a => a.ReceivedAt >= cutoff)) _queue.Enqueue(a);
        }
    }
}
