using ArticleService.Models;

namespace ArticleService.Infrastructure.Interface
{
    public interface IConnectionStringResolver
    {
        string GetConnectionStringForContinent(Continent continent);
        string GetConnectionStringForGlobal();
    }
}
