using DraftService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DraftService.Infrastructure
{
    public class DesignTimeDraftDbContextFactory : IDesignTimeDbContextFactory<DraftDbContext>
    {
        public DraftDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables().Build();

            var connectionstring = config.GetConnectionString("DraftDatabase");

            var builder = new DbContextOptionsBuilder<DraftDbContext>();
            builder.UseSqlServer(connectionstring);
            return new DraftDbContext(builder.Options);
        }
    }
}
