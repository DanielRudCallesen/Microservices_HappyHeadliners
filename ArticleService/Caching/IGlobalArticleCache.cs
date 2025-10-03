using ArticleService.Models;

namespace ArticleService.Caching
{
    public interface IGlobalArticleCache
    {
        Task<(ArticleReadDTO? value, bool hit)> TryGet(int id, CancellationToken ct);
        Task<IReadOnlyList<ArticleReadDTO>> GetRecentPage(int skip, int take, CancellationToken ct);
        Task Upsert(ArticleReadDTO dto, CancellationToken ct);
        Task Invalidate(int id, CancellationToken ct);
    }
}
