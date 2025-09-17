using ArticleService.Data;
using ArticleService.Infrastructure.Interface;
using ArticleService.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Infrastructure
{
    public class ShardMigratorHostedService(IConnectionStringResolver resolver, ILogger<ShardMigratorHostedService> logger) : IHostedService
    {
        private readonly IConnectionStringResolver _resolver = resolver;
        private readonly ILogger<ShardMigratorHostedService> _logger = logger;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Had to add delay, because the mirgation only succussed on the 8th try when server was ready.
            try { await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken); } catch {}

            var targets = new List<(string Name, string? Cs)>();
            string? Try(Func<string> get, string name)
            {
                try { return get(); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Connection string for {Name} missing. Skipping migration.", name);
                    return null;
                }
            }

            targets.Add(("Global", Try(_resolver.GetConnectionStringForGlobal, "Global")));

            foreach (var c in Enum.GetValues<Continent>())
                targets.Add(($"{c}", Try(() => _resolver.GetConnectionStringForContinent(c), $"{c}")));

            foreach (var (name, cs) in targets.Where(t => !string.IsNullOrWhiteSpace(t.Cs)))
            {
                var csb = new SqlConnectionStringBuilder(cs);
                _logger.LogInformation("Migrating {Name}: Datasource={DataSource} Database={Db}", name, csb.DataSource, csb.InitialCatalog);
                var attempts = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var options = new DbContextOptionsBuilder<ArticleDbContext>()
                            .UseSqlServer(cs!, o => o.EnableRetryOnFailure())
                            .Options;

                        using var db = new ArticleDbContext(options);
                        await db.Database.MigrateAsync(cancellationToken);
                        _logger.LogInformation("Database migrated: {Name}", name);
                        break;
                    }
                    catch (Exception ex) when (attempts++ < 8)
                    {
                        attempts++;
                        _logger.LogWarning(ex, "Migrate attempt {Attempt} failed for {Name}. Retrying...", attempts, name);
                        try
                        {
                            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                        }
                        catch {}

                    }
                    catch (Exception ex) when (attempts++ < 8)
                    {
                        _logger.LogWarning(ex, "Migrate attempt {Attempt} failed for {Name}. Retrying...", attempts, name);
                        try { await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempts))), cancellationToken); } catch { }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Migration failed for {Name}. Giving up.", name);
                        break;
                    }
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
