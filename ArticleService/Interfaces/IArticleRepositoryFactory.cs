using ArticleService.Models;

namespace ArticleService.Interfaces
{
    public interface IArticleRepositoryFactory
    {
        IArticleRepository CreateGlobal();
        IArticleRepository CreateForContinent(Continent continent);
    }
}
