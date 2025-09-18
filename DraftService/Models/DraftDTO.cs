namespace DraftService.Models
{
    public record DraftSnapshotRequest(int ArticleId, string Title, string Content, string? Continent);

    public record DraftReadDto(
        int Id,
        int ArticleId,
        string Title,
        string Content,
        int Version,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        string? Continent);
}
