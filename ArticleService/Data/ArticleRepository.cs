using ArticleService.Interfaces;
using ArticleService.Models;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Data
{
    public class ArticleRepository(ArticleDbContext context) : IArticleRepository
    {
        private readonly ArticleDbContext _context = context;

        public Task<Article?> GetAsync(int id, CancellationToken cancellationToken) =>
        _context.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        public Task<List<Article>> GetPagedAsync(int skip, int take, CancellationToken cancellationToken) =>
        _context.Articles.OrderByDescending(a => a.PublishedDate).Skip(skip).Take(take).ToListAsync(cancellationToken);

        public async Task<Article> AddAsync(Article article, CancellationToken cancellationToken)
        {
            _context.Articles.Add(article);
            await _context.SaveChangesAsync(cancellationToken);
            return article;
        }

        public async Task UpdateAsync(Article article, CancellationToken cancellationToken)
        {
            _context.Articles.Update(article);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteAsync(Article article, CancellationToken cancellationToken)
        {
            _context.Articles.Remove(article);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
