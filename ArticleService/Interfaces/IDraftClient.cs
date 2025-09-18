using ArticleService.Models;

namespace ArticleService.Interfaces
{
    public interface IDraftClient
    {
        Task SaveSnapshotAsync(ArticleReadDTO article, CancellationToken ct);
    }
}
