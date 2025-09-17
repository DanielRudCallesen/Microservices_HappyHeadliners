namespace CommentService.Models
{
    public class CommentDTO
    {
        public record CommentCreateDto(int ArticleId, string UserId, string UserName, string Content);

        public record CommentReadDto(
            int Id,
            int ArticleId,
            string UserId,
            string UserName,
            string Content,
            DateTime CreatedAt,
            DateTime? UpdatedAt);

        public record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
    }
}
