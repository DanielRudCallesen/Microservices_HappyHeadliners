using CommentService.Caching;
using CommentService.Data;
using CommentService.Interface;
using CommentService.Models;
using Microsoft.EntityFrameworkCore;

namespace CommentService.Services
{
    public class CommentService(CommentDbContext db, IProfanityClient profanityClient, ILocalProfanityFilter fallback, ILogger<CommentService> logger, IArticleExistenceClient articleClient, ICommentCache commentCache, IConfiguration config) : ICommentService
    {
        private readonly CommentDbContext _db = db;
        private readonly IProfanityClient _profanity = profanityClient;
        private readonly ILocalProfanityFilter _fallback = fallback;
        private readonly ILogger<CommentService> _logger = logger;
        private readonly IArticleExistenceClient _articleClient = articleClient;
        private readonly ICommentCache _cache = commentCache;
        private readonly bool _cacheEnabled = config.GetValue("CommentCache:Enabled", true);

        public async Task<CommentDTO.CommentReadDto> CreateAsync(CommentDTO.CommentCreateDto dto, CancellationToken ct)
        {
            if(dto.ArticleId <= 0) throw new ArgumentException("ArticleId must be greater than zero");
            if (string.IsNullOrWhiteSpace(dto.UserId)) throw new ArgumentException("UserId is required");
            if (string.IsNullOrWhiteSpace(dto.UserName)) throw new ArgumentException("UserName is required");
            if (string.IsNullOrWhiteSpace(dto.Content)) throw new ArgumentException("Content is required");


            var exists = await _articleClient.Exists(dto.ArticleId, dto.Continent, ct);
            if (!exists)
            {
                throw new ArticleNotFoundException(dto.ArticleId);
            }

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

            var created = new CommentDTO.CommentReadDto(entity.Id, entity.ArticleId, entity.UserId, entity.UserName,
                entity.Content, entity.CreatedAt, entity.UpdatedAt);

            if (_cacheEnabled) await _cache.AppendIfPresent(entity.ArticleId, created, ct);

            return created;

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

            IReadOnlyList<CommentDTO.CommentReadDto>? all = null;
            if (_cacheEnabled)
            {
                var (cached, hit) = await _cache.TryGetAll(articleId, ct);
                if (hit)
                {
                    all = cached!;
                    var itemsC = all.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToList();
                    return new CommentDTO.PageResult<CommentDTO.CommentReadDto>(itemsC, page, pageSize, all.Count);
                }
            }
            // Cache miss: Load full list, store, then page in memory
            var query = _db.Comments.AsNoTracking().Where( c => c.ArticleId == articleId).OrderByDescending(c => c.CreatedAt);

            var list = await query.ToListAsync(ct);
            var dtos = list.Select(c =>
                new CommentDTO.CommentReadDto(c.Id, c.ArticleId, c.UserId, c.UserName, c.Content, c.CreatedAt,
                    c.UpdatedAt)).ToList();
            all = dtos;

            if (_cacheEnabled) await _cache.StoreAll(articleId, all, ct);

            var items = all.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToList();
            

            return new CommentDTO.PageResult<CommentDTO.CommentReadDto>(items, page, pageSize, all.Count);
        }
    }
}
