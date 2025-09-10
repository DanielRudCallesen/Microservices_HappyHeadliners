using ArticleService.Models;

namespace ArticleService.Interfaces
{
    public interface IArticleRepository
    {
        Task<Article?> GetAsync(int id, CancellationToken cancellationToken);
        Task<List<Article>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken);
        Task<Article> AddAsync(Article article, CancellationToken cancellationToken);
        Task UpdateAsync(Article article, CancellationToken cancellationToken);
        Task DeleteAsync(Article article, CancellationToken cancellationToken);
    }
}
