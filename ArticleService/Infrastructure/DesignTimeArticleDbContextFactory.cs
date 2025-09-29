using ArticleService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ArticleService.Infrastructure
{
    public class DesignTimeArticleDbContextFactory : IDesignTimeDbContextFactory<ArticleDbContext>
    {
        public ArticleDbContext CreateDbContext(string[] args)
        {
            // Load configuration (looks in current dir for appsettings.json)
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            
            var cs = config.GetConnectionString("Global")
                     ?? throw new InvalidOperationException("GlobalDatabase connection string missing for design-time.");

            var builder = new DbContextOptionsBuilder<ArticleDbContext>();
            builder.UseSqlServer(cs);

            return new ArticleDbContext(builder.Options);
        }
    }
}
