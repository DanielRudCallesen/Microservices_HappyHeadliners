using CommentService.Models;

namespace CommentService.Caching
{
    public interface ICommentCache
    {
        Task<(IReadOnlyList<CommentDTO.CommentReadDto>? value, bool hit)>
            TryGetAll(int articleId, CancellationToken ct);

        Task StoreAll(int articleId, IReadOnlyList<CommentDTO.CommentReadDto> list, CancellationToken ct);

        Task AppendIfPresent(int articleId, CommentDTO.CommentReadDto dto, CancellationToken ct);
    }
}
