using ArticleService.Models;

namespace ArticleService.Interfaces
{
    public interface IArticleService
    {
        Task<ArticleReadDTO> CreateAsync(ArticleCreateDTO dto, CancellationToken cancellationToken);
        Task<ArticleReadDTO?> GetAsync(int id, Continent? continent, bool includeGlobalFallback, CancellationToken cancellationToken);
        Task<IReadOnlyList<ArticleReadDTO>> GetListAsync(Continent? continent,int page, int pageSize, bool includeGlobal, CancellationToken cancellationToken);
        Task<bool> UpdateAsync(int id, Continent? continent, ArticleUpdateDTO dto, CancellationToken cancellationToken);
        Task<bool> DeleteAsync(int id, Continent? continent, CancellationToken cancellationToken);

        Task<ArticleReadDTO> CreateFromEvent(Guid correlationId, string title, string content, Continent? continent,
            CancellationToken cancellationToken);
    }
}
