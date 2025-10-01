using CommentService.Data;
using CommentService.Interface;
using CommentService.Models;
using Microsoft.EntityFrameworkCore;

namespace CommentService.Services
{
    public class CommentService(CommentDbContext db, IProfanityClient profanityClient, ILocalProfanityFilter fallback, ILogger<CommentService> logger) : ICommentService
    {
        private readonly CommentDbContext _db = db;
        private readonly IProfanityClient _profanity = profanityClient;
        private readonly ILocalProfanityFilter _fallback = fallback;
        private readonly ILogger<CommentService> _logger = logger;

        public async Task<CommentDTO.CommentReadDto> CreateAsync(CommentDTO.CommentCreateDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.UserId)) throw new ArgumentException("UserId is required");
            if (string.IsNullOrWhiteSpace(dto.UserName)) throw new ArgumentException("UserName is required");
            if (string.IsNullOrWhiteSpace(dto.Content)) throw new ArgumentException("Content is required");

            string santizied;
            bool hasProfanity;

           
            try
            {
                var result = await _profanity.FilterAsync(dto.Content, ct);
                santizied = result.SanitizedContent;
                hasProfanity = result.HasProfanity;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ProfanityService unavailable. Using local fallback.");
                santizied= _fallback.Sanitize(dto.Content);
                hasProfanity = !string.Equals(dto.Content, santizied, StringComparison.Ordinal);
            }

            if (hasProfanity)
            {
                _logger.LogInformation("Profanity detected and santized for ArticleId={ArticleId}", dto.ArticleId);
            }

            var entity = new Comment
            {
                ArticleId = dto.ArticleId,
                UserId = dto.UserId,
                UserName = dto.UserName,
                Content = santizied,
                CreatedAt = DateTime.UtcNow
            };
            _db.Comments.Add(entity);
            await _db.SaveChangesAsync(ct);

            return new CommentDTO.CommentReadDto(entity.Id, entity.ArticleId, entity.UserId, entity.UserName, entity.Content, entity.CreatedAt, entity.UpdatedAt);
                
        }

        public async Task<CommentDTO.CommentReadDto?> GetAsync(int id, CancellationToken ct)
        {
            var c = await _db.Comments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
            return c is null ? null : new CommentDTO.CommentReadDto(c.Id, c.ArticleId, c.UserId, c.UserName, c.Content,
                c.CreatedAt, c.UpdatedAt);
        }

        public async Task<CommentDTO.PageResult<CommentDTO.CommentReadDto>> GetByArticleAsync(int articleId, int page,
            int pageSize, CancellationToken ct)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _db.Comments.AsNoTracking().Where( c => c.ArticleId == articleId).OrderByDescending(c => c.CreatedAt);

            var total = await query.CountAsync(ct);
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(c =>
                new CommentDTO.CommentReadDto(c.Id, c.ArticleId, c.UserId, c.UserName, c.Content, c.CreatedAt,
                    c.UpdatedAt)).ToListAsync(ct);

            return new CommentDTO.PageResult<CommentDTO.CommentReadDto>(items, page, pageSize, total);
        }
    }
}
