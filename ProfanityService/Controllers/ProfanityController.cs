using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProfanityService.Data;
using ProfanityService.Models;

namespace ProfanityService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProfanityController(ProfanityDbContext db, ILogger<ProfanityController> logger) : ControllerBase
    {
        private readonly ILogger<ProfanityController> _logger = logger;
        private readonly ProfanityDbContext _db = db;

        public record FilterRequest(string Content);

        public record FilterResponse(string SanitizedContent, bool HadProfanity, string[] Matches);

        [HttpPost("filter")]
        public async Task<IActionResult> Filter([FromBody] FilterRequest request, CancellationToken ct)
        {
            var words = await _db.Words.AsNoTracking().Select(w => w.Word).ToListAsync(ct);

            if (words.Count == 0)
            {
                return Ok(new FilterResponse(request.Content, false, Array.Empty<string>()));
            }

            var escaped = words.Select(Regex.Escape);
            var pattern = $@"\b({string.Join("|", escaped)})\b";
            var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string sanitized = Regex.Replace(request.Content, pattern, m =>
            {
                matches.Add(m.Value);
                return new string('*', m.Value.Length);
            }, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            return Ok(new FilterResponse(sanitized, matches.Count > 0, matches.ToArray()));
        }

        [HttpGet("words")]
        public async Task<IActionResult> GetWords(CancellationToken ct) =>
            Ok(await _db.Words.AsNoTracking().OrderBy(w => w.Word).Select(w => w.Word).ToListAsync(ct));

        public record UpsertWordRequest(string word);

        [HttpPost("words")]
        public async Task<IActionResult> AddWord([FromBody] UpsertWordRequest request, CancellationToken ct)
        {
            var word = (request.word ?? String.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(word)) return BadRequest("Words are required");

            if (await _db.Words.AnyAsync(w => w.Word == word, ct)) return Conflict("World already exists");

            _db.Words.Add(new ProfanityWord { Word = word });
            await _db.SaveChangesAsync(ct);
            return CreatedAtAction(nameof(GetWords), null);
        }

        [HttpDelete("words/{word}")]
        public async Task<IActionResult> DeleteWord([FromRoute] string word, CancellationToken ct)
        {
            var trimlower = (word ?? string.Empty).Trim().ToLowerInvariant();
            var entity = await _db.Words.FirstOrDefaultAsync(w => w.Word == trimlower, ct);
            if (entity is null ) return NotFound("Word not found");

            _db.Words.Remove(entity);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

    }
}
