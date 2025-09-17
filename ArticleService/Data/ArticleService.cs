using ArticleService.Interfaces;
using ArticleService.Models;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Data
{
    public class ArticleService(IArticleRepositoryFactory factory, ILogger<ArticleService> logger) : IArticleService
    {
        private readonly IArticleRepositoryFactory _factory = factory;
        private readonly ILogger<ArticleService> _logger = logger;

        public async Task<ArticleReadDTO> CreateAsync(ArticleCreateDTO dto, CancellationToken ct)
        {
            var repository = dto.Continent is null ? _factory.CreateGlobal() : _factory.CreateForContinent(dto.Continent.Value);

            var entity = new Article
            {
                Title = dto.Title,
                Content = dto.Content,
                Continent = dto.Continent,
                PublishedDate = DateTime.UtcNow
            };

            entity = await repository.AddAsync(entity, ct);

            _logger.LogInformation("Created article {ArticleId} in {Continent} repository", entity.Id, dto.Continent?.ToString() ?? "Global");

            return Map(entity);
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
