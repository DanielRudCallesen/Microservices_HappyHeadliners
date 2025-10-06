using ArticleService.Caching;
using ArticleService.Interfaces;
using ArticleService.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Messaging.ArticleQueue.Model;

namespace ArticleService.Data
{
    public class ArticleService(IArticleRepositoryFactory factory, ILogger<ArticleService> logger, IArticleCache cache, IConfiguration config) : IArticleService
    {
        private readonly IArticleRepositoryFactory _factory = factory;
        private readonly ILogger<ArticleService> _logger = logger;

        private readonly IArticleCache _cache = cache;

        private readonly bool _cacheEnabled = config.GetValue("ArticleCache:Enabled", true);
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
                var existingDto = Map(existing);
                if (_cacheEnabled) await _cache.Upsert(existingDto, continent, ct);
                return existingDto;
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
                var dto = Map(entity);
                if (_cacheEnabled) await _cache.Upsert(dto, continent, ct);
                _logger.LogInformation("Persisted event article CorrelationId={CorrelationId} ArticleId={ArticleId}",
                    correlationId, entity.Id);
                return dto;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Race on CorrelationId={CorrelationId}, realoding", correlationId);
                if (repo is ArticleRepository ar2 && await ar2.TryGetByCorrelationId(correlationId, ct) is { } raced)
                {
                    var dto = Map(raced);
                    if (_cacheEnabled) await _cache.Upsert(dto, continent, ct);
                    return dto;
                }
                throw;
            }
        }
        
        
        public async Task<ArticleReadDTO?> GetAsync(int id, Continent? continent, bool includeGlobalFallBack, CancellationToken ct)
        {
            if (_cacheEnabled)
            {
                var (cached, hit) = await _cache.TryGet(id, continent, ct);
                if (hit) return cached;
            }

            if (continent is not null)
            {
                var continentRepo = _factory.CreateForContinent(continent.Value);
                var fromContinent = await continentRepo.GetAsync(id, ct);
                if (fromContinent is not null)
                {
                    var dto = Map(fromContinent);
                    if (_cacheEnabled) await _cache.Upsert(dto, continent, ct);
                    return dto;
                }
                

                if(includeGlobalFallBack)
                {
                    
                     return await GetAsync(id, null, false, ct);
                    
                }

                return null;
            }
            
            // If no continent provided: use Global
            var repo = _factory.CreateGlobal();
            var article = await repo.GetAsync(id, ct);
            if (article is null) return null;
            var globalDto = Map(article);
            if (_cacheEnabled) await _cache.Upsert(globalDto, null, ct);
            return globalDto;
        }

        public async Task<IReadOnlyList<ArticleReadDTO>> GetListAsync(Continent? continent, int page, int pageSize, bool includeGlobal, CancellationToken ct)
        {
            var skip = (page - 1) * pageSize;
            

            if (continent is not null)
            {
                if (_cacheEnabled)
                {
                    var cached = await _cache.GetRecent(null, skip, pageSize, ct);
                    if (cached.Count > 0) return cached.ToList();
                }
                var repo = _factory.CreateGlobal();
                var onlyGlobal = await repo.GetPagedAsync(skip, pageSize, ct);
                var list = onlyGlobal.Select(Map).ToList();
                if (_cacheEnabled) foreach (var a in list) await _cache.Upsert(a, null, ct);
                return list;
            }

            // Continent path
            IReadOnlyList<ArticleReadDTO> continentSlice;
            if (_cacheEnabled)
            {
                continentSlice = await _cache.GetRecent(continent, skip, pageSize, ct);
                if (continentSlice.Count == 0)
                {
                    var cRepo = _factory.CreateForContinent(continent.Value);
                    var fromDb = await cRepo.GetPagedAsync(skip, pageSize, ct);
                    continentSlice = fromDb.Select(Map).ToList();
                }
            }
            else
            {
                var cRepo = _factory.CreateForContinent(continent.Value);
                var fromDb = await cRepo.GetPagedAsync(skip, pageSize, ct);
                continentSlice = fromDb.Select(Map).ToList();
            }

            if (!includeGlobal)
                return continentSlice.ToList();

            IReadOnlyList<ArticleReadDTO> globalSlice;
            if (_cacheEnabled)
            {
                globalSlice = await _cache.GetRecent(null, skip, pageSize, ct);
                if (globalSlice.Count == 0)
                {
                    var gRepo = _factory.CreateGlobal();
                    globalSlice = (await gRepo.GetPagedAsync(skip, pageSize, ct)).Select(Map).ToList();
                }
            }
            else
            {
                var gRepo = _factory.CreateGlobal();
                globalSlice = (await gRepo.GetPagedAsync(skip, pageSize, ct)).Select(Map).ToList();
            }

            return continentSlice.Concat(globalSlice).OrderByDescending(a => a.PublishedDate).Take(pageSize).ToList();
        }

        public async Task<bool> UpdateAsync(int id, Continent? continent, ArticleUpdateDTO dto, CancellationToken ct)
        {
            var repo = continent is null? _factory.CreateGlobal() : _factory.CreateForContinent(continent.Value);
            var existing = await repo.GetAsync(id, ct);
            if (existing is null) return false;

            existing.Title = dto.Title;
            existing.Content = dto.Content;

            await repo.UpdateAsync(existing, ct);
            if (_cacheEnabled) await _cache.Invalidate(id, continent, ct);
            return true;
        }

        public async Task<bool> DeleteAsync(int id, Continent? continent, CancellationToken ct)
        {
            var repo = continent is null ? _factory.CreateGlobal() : _factory.CreateForContinent(continent.Value);
            var existing = await repo.GetAsync(id, ct);
            if (existing is null) return false;

            await repo.DeleteAsync(existing, ct);
            if (_cacheEnabled) await _cache.Invalidate(id, continent, ct);
            return true;
        }

        private static ArticleReadDTO Map(Article a) => new(a.Id, a.Title, a.Content, a.PublishedDate, a.Continent);
    }
}
