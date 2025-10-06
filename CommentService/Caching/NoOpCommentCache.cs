using CommentService.Models;

namespace CommentService.Caching
{
    public sealed class NoOpCommentCache : ICommentCache
    {
        public Task<(IReadOnlyList<CommentDTO.CommentReadDto>? value, bool hit)> TryGetAll(int articleId, CancellationToken ct)
            => Task.FromResult<(IReadOnlyList<CommentDTO.CommentReadDto>?, bool)>((null, false));

        public Task StoreAll(int articleId, IReadOnlyList<CommentDTO.CommentReadDto> list, CancellationToken ct)
            => Task.CompletedTask;

        public Task AppendIfPresent(int articleId, CommentDTO.CommentReadDto dto, CancellationToken ct)
            => Task.CompletedTask;
    }
}
