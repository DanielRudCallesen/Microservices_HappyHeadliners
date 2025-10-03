using ArticleService.Interfaces;
using ArticleService.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Messaging.ArticleQueue.Model;

namespace ArticleService.Data
{
    public class ArticleService(IArticleRepositoryFactory factory, ILogger<ArticleService> logger) : IArticleService
    {
        private readonly IArticleRepositoryFactory _factory = factory;
        private readonly ILogger<ArticleService> _logger = logger;
        //private readonly Shared.Messaging.ArticleQueue.Interface.IArticleQueue _articleQueue = articleQueue;

        // Removed CreateAsync - Added PersistFromEvent instead
        //public async Task<ArticleReadDTO> CreateAsync(ArticleCreateDTO dto, CancellationToken ct)
        //{
        //    var repository = dto.Continent is null ? _factory.CreateGlobal() : _factory.CreateForContinent(dto.Continent.Value);

        //    var correlationId = Guid.NewGuid();
        //    var entity = new Article
        //    {
        //        Title = dto.Title,
        //        Content = dto.Content,
        //        Continent = dto.Continent,
        //        PublishedDate = DateTime.UtcNow,
        //        CorrelationId = correlationId
        //    };

        //    entity = await repository.AddAsync(entity, ct);

        //    await _articleQueue.PublishAsync(new PublishedArticle
        //    {
        //        CorrelationId = correlationId,
        //        Title = entity.Title,
        //        Content = entity.Content,
        //        Author = "Unknown", // map as needed
        //        Continent = entity.Continent?.ToString() ?? "Global",
        //        PublishedAt = DateTimeOffset.UtcNow
        //    }, ct);

        //    _logger.LogInformation("Created article {ArticleId} in {Continent} repository", entity.Id, dto.Continent?.ToString() ?? "Global");

        //    return Map(entity);
        //}

        

        public async Task<ArticleReadDTO> PersistFromEventAsync(Guid correlationId, string title, string content,
            Continent? continent, DateTime publishedDate, CancellationToken ct)
        {
            var repo = continent is null ? _factory.CreateGlobal() : _factory.CreateForContinent(continent.Value);

            if (repo is ArticleRepository ar && await ar.TryGetByCorrelationId(correlationId, ct) is { } existing)
            {
                _logger.LogInformation("Event article already exists CorrelationId={CorrelationId} ArticleId={ArticleId}", correlationId, existing.Id);
                return Map(existing);
            }

            var entity = new Article
            {
                Title = title,
                Content = content,
                Continent = continent,
                PublishedDate = publishedDate,
                CorrelationId = correlationId
            };

            try
            {
                entity = await repo.AddAsync(entity, ct);
                _logger.LogInformation("Persisted event article CorrelationId={CorrelationId} ArticleId={ArticleId}",
                    correlationId, entity.Id);
                return Map(entity);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Race on CorrelationId={CorrelationId}, realoding", correlationId);
                if (repo is ArticleRepository ar2 && await ar2.TryGetByCorrelationId(correlationId, ct) is { } raced)
                    return Map(raced);
                throw;
            }
        }
        
        
        public async Task<ArticleReadDTO?> GetAsync(int id, Continent? continent, bool includeGlobalFallBack, CancellationToken ct)
        {
            if (continent is not null)
            {
                var continentRepo = _factory.CreateForContinent(continent.Value);
                var fromContinent = await continentRepo.GetAsync(id, ct);
                if (fromContinent is not null) return Map(fromContinent);
                

                if(includeGlobalFallBack)
                {
                    var globalRepo = _factory.CreateGlobal();
                    var globalFound = await globalRepo.GetAsync(id, ct);
                    return globalFound is null ? null : Map(globalFound);
                }

                return null;
            }
            
            // If no continent provided: use Global
            var repo = _factory.CreateGlobal();
            var article = await repo.GetAsync(id, ct);
            return article is null ? null : Map(article);
        }

        public async Task<IReadOnlyList<ArticleReadDTO>> GetListAsync(Continent? continent, int page, int pageSize, bool includeGlobal, CancellationToken ct)
        {
            var skip = (page - 1) * pageSize;
            var list = new List<ArticleReadDTO>();

            if (continent is not null)
            {
                var continentRepo = _factory.CreateForContinent(continent.Value);
                var continentItems = await continentRepo.GetPagedAsync(skip, pageSize, ct);
                list.AddRange(continentItems.Select(Map));

                if (includeGlobal)
                {
                    var globalRepo = _factory.CreateGlobal();
                    var globalItems = await globalRepo.GetPagedAsync(skip, pageSize, ct);
                    list.AddRange(globalItems.Select(Map));
                }

                list = list.OrderByDescending(a => a.PublishedDate).Take(pageSize).ToList();
                return list;
            }
            
            var repo = _factory.CreateGlobal();
            var onlyGlobal = await repo.GetPagedAsync(skip, pageSize, ct);
            return onlyGlobal.Select(Map).ToList();
        }

        public async Task<bool> UpdateAsync(int id, Continent? continent, ArticleUpdateDTO dto, CancellationToken ct)
        {
            var repo = continent is null? _factory.CreateGlobal() : _factory.CreateForContinent(continent.Value);
            var existing = await repo.GetAsync(id, ct);
            if (existing is null) return false;

            existing.Title = dto.Title;
            existing.Content = dto.Content;

            await repo.UpdateAsync(existing, ct);
            return true;
        }

        public async Task<bool> DeleteAsync(int id, Continent? continent, CancellationToken ct)
        {
            var repo = continent is null ? _factory.CreateGlobal() : _factory.CreateForContinent(continent.Value);
            var existing = await repo.GetAsync(id, ct);
            if (existing is null) return false;

            await repo.DeleteAsync(existing, ct);
            return true;
        }

        private static ArticleReadDTO Map(Article a) => new(a.Id, a.Title, a.Content, a.PublishedDate, a.Continent);
    }
}
