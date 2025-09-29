using NewsletterService.Model;

namespace NewsletterService.Services
{
    public interface IImmediateArticleStore
    {
        void Add(ImmediateArticle article);
        IReadOnlyCollection<ImmediateArticle> GetLatest(int max = 50);
        void Prune(TimeSpan maxAge);
    }
}
