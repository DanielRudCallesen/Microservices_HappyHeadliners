using ArticleService.Models;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Data
{
    public class ArticleDbContext(DbContextOptions<ArticleDbContext> options) : DbContext(options)
    {
        public DbSet<Article> Articles => Set<Article>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Article>().Property(a => a.PublishedDate)
                .HasConversion(d => d, d => DateTime.SpecifyKind(d, DateTimeKind.Utc));
            modelBuilder.Entity<Article>().HasIndex(a => a.PublishedDate);
            modelBuilder.Entity<Article>().HasIndex(a => a.CorrelationId).IsUnique();
        }
    }
}
