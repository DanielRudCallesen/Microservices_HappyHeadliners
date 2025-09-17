using ArticleService.Infrastructure.Interface;
using ArticleService.Models;

namespace ArticleService.Infrastructure
{
    public class ConfigurationConnectionString(IConfiguration configuration) : IConnectionStringResolver
    {
        private readonly IConfiguration _configuration = configuration;

        public string GetConnectionStringForGlobal() =>
            _configuration.GetConnectionString("Global") 
            ?? throw new InvalidOperationException("Global database connection string is not configured.");

        public string GetConnectionStringForContinent(Continent continent)
        {
            var key = $"Articles:{continent}";
            return _configuration.GetConnectionString(key) 
                ?? throw new InvalidOperationException($"Connection string for continent '{continent}' is not configured.");
        }
    }
}
