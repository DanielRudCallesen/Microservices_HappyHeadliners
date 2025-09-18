using DraftService.Models;
using Microsoft.EntityFrameworkCore;

namespace DraftService.Data
{
    public class DraftDbContext(DbContextOptions<DraftDbContext> options) : DbContext(options)
    {
        public DbSet<Draft> Drafts => Set<Draft>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Draft>(d =>
            {
                d.HasIndex(d => d.ArticleId);
                d.Property(d => d.CreatedAt).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
                d.Property(d => d.UpdatedAt).HasConversion(v => v, v => v == default ? null : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));
                d.Property(d => d.ContentHash).HasMaxLength(64);
            });
        }
    }
}
