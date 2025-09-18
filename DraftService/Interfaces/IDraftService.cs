using DraftService.Models;

namespace DraftService.Interfaces
{
    public interface IDraftService
    {
        Task<DraftReadDto> SaveSnapshotAsync(DraftSnapshotRequest request, CancellationToken ct);
        Task<IReadOnlyList<DraftReadDto>> GetByArticleAsync(int articleId, CancellationToken ct);
        Task<DraftReadDto?> GetAsync(int id, CancellationToken ct);
    }
}
