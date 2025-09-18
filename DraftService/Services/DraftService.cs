using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using DraftService.Data;
using DraftService.Interfaces;
using DraftService.Models;
using Microsoft.EntityFrameworkCore;

namespace DraftService.Services
{
    public class DraftService(DraftDbContext db, ILogger<DraftService> logger) : IDraftService
    {
        private readonly DraftDbContext _db = db;
        private readonly ILogger<DraftService> _logger = logger;
        private static readonly ActivitySource activitySource = new("DraftService");

        public async Task<DraftReadDto> SaveSnapShotAsync(DraftSnapshotRequest request, CancellationToken ct)
        {
            using var activity = activitySource.StartActivity("SaveSnapShotAsync", ActivityKind.Internal);
            activity?.SetTag("article.id", request.ArticleId);

            var hash = ComputeHash(request.Content);
            var lastest = await _db.Drafts.Where(d => d.ArticleId == request.ArticleId)
                .OrderByDescending(d => d.Version).FirstOrDefaultAsync(ct);

            if (lastest is not null & lastest.ContentHash == hash)
            {
                _logger.LogInformation("Skipping draft snapshot (Unchanged) articleId={ArticleId} Version={Version}", request.ArticleId, lastest.Version );
                activity?.SetTag("draft.skipped", true);
                return Map(lastest);
            }

            var nextVersion = (lastest?.Version ?? 0) + 1;
            var entity = new Draft
            {
                ArticleId = request.ArticleId;
                Title = request.Title,
                Content = request.Content,
                Version = nextVersion,
                Continent = request.Continent,
                ContentHash = hash
            };

            _db.Drafts.Add(entity);
            await _db.SaveChangesAsync(ct);
            
            _logger.LogInformation("Draft snapshot saved ArticleId={ArticleId} Version={Version} DraftId={DraftId}", entity.ArticleId, entity.Version, entity.Id);
            return Map(entity);
        }

        public async Task<IReadOnlyList<DraftReadDto>> GetByArticleAsync(int articleId, CancellationToken ct)
        {
            var list = await _db.Drafts.AsNoTracking()
                .Where(d => d.ArticleId == articleId)
                .OrderByDescending(d => d.Version)
                .ToListAsync(ct);
            return list.Select(Map).ToList();
        }

        public async Task<DraftReadDto?> GetAsync(int id, CancellationToken ct)
        {
            var d = await _db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
            return d is null ? null : Map(d);
        }

        private static DraftReadDto Map(Draft d) => new(d.Id, d.ArticleId, d.Title, d.Content, d.Version, d.CreatedAt,
            d.UpdatedAt, d.Continent);

        private static string ComputeHash(string content)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            return Convert.ToHexString(bytes);
        }
    }
}
