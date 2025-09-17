using CommentService.Models;

namespace CommentService.Interface
{
    public interface ICommentService
    {
        Task<CommentDTO.CommentReadDto> CreateAsync(CommentDTO.CommentCreateDto dto, CancellationToken ct);
        Task<CommentDTO.CommentReadDto?> GetAsync(int id, CancellationToken ct);
        Task<CommentDTO.PageResult<CommentDTO.CommentReadDto>> GetByArticleAsync(int articleId, int page, int pageSize, CancellationToken ct);

    }
}
