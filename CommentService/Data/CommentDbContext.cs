using CommentService.Models;
using Microsoft.EntityFrameworkCore;

namespace CommentService.Data
{
    public class CommentDbContext(DbContextOptions<CommentDbContext> options) : DbContext(options)
    {
        public DbSet<Comment> Comments => Set<Comment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Comment>(entity =>
            {
                entity.ToTable("Comments");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ArticleId).IsRequired();
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.UserName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Content).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.CreatedAt).IsRequired();
                entity.Property(e => e.UpdatedAt);
                entity.HasIndex(e => e.ArticleId);
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }
}
