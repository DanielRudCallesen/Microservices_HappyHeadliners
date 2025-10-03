using ArticleService.Models;

namespace ArticleService.Caching
{
    public sealed class NoOpGlobalArticleCache : IGlobalArticleCache
    {
        public Task<(ArticleReadDTO? value, bool hit)> TryGet(int id, CancellationToken ct) =>
            Task.FromResult<(ArticleReadDTO?, bool)>((null, false));

        public Task<IReadOnlyList<ArticleReadDTO>> GetRecentPage(int skip, int take, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ArticleReadDTO>>(Array.Empty<ArticleReadDTO>());

        public Task Upsert(ArticleReadDTO dto, CancellationToken ct) => Task.CompletedTask;
        public Task Invalidate(int id, CancellationToken ct) => Task.CompletedTask;

    }
}
