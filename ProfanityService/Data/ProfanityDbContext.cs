using Microsoft.EntityFrameworkCore;

using ProfanityService.Models;

namespace ProfanityService.Data
{
    public class ProfanityDbContext(DbContextOptions<ProfanityDbContext> options) : DbContext(options)
    {
        public DbSet<ProfanityWord> Words => Set<ProfanityWord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProfanityWord>(b =>
            {
                b.ToTable("ProfanityWords");
                b.HasKey(x => x.Id);
                b.Property(x => x.Word).IsRequired().HasMaxLength(128);
                b.HasIndex(x => x.Word).IsUnique();
            });
        }
    }
}
