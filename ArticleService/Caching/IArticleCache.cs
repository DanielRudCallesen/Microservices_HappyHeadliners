using ArticleService.Models;

namespace ArticleService.Caching
{
    public interface IArticleCache
    {
        Task<(ArticleReadDTO? value, bool hit)> TryGet(int id, Continent? continent, CancellationToken ct);
        Task<IReadOnlyList<ArticleReadDTO>> GetRecent(Continent? continent, int skip, int take, CancellationToken ct);
        Task Upsert(ArticleReadDTO dto, Continent? continent, CancellationToken ct);
        Task Invalidate(int id, Continent? continent, CancellationToken ct);
    }
}
