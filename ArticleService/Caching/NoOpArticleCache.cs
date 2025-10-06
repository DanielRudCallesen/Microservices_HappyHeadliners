using ArticleService.Models;

namespace ArticleService.Caching
{
    public sealed class NoOpArticleCache : IArticleCache
    {
        public Task<(ArticleReadDTO? value, bool hit)> TryGet(int id, Continent? continent, CancellationToken ct) =>
            Task.FromResult<(ArticleReadDTO?, bool)>((null, false));

        public Task<IReadOnlyList<ArticleReadDTO>> GetRecent(Continent? continent, int skip, int take, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ArticleReadDTO>>(Array.Empty<ArticleReadDTO>());

        public Task Upsert(ArticleReadDTO dto, Continent? continent, CancellationToken ct) => Task.CompletedTask;
        public Task Invalidate(int id, Continent? continent, CancellationToken ct) => Task.CompletedTask;

    }
}
