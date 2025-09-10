using ArticleService.Infrastructure.Interface;
using ArticleService.Interfaces;
using ArticleService.Models;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Data
{
    public class ArticleRepositoryFactory(IConnectionStringResolver Resolver) : IArticleRepositoryFactory
    {
        private readonly IConnectionStringResolver _resolver = Resolver;


        private ArticleDbContext BuildContext(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ArticleDbContext>().UseSqlServer(connectionString).Options;

            return new ArticleDbContext(optionsBuilder);
        }

        public IArticleRepository CreateForContinent(Continent continent) => new ArticleRepository(BuildContext(_resolver.GetConnectionStringForContinent(continent)));

        public IArticleRepository CreateGlobal() => new ArticleRepository(BuildContext(_resolver.GetConnectionStringForGlobal()));
    }
}
