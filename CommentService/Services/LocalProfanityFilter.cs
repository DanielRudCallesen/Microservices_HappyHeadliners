using CommentService.Interface;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CommentService.Services
{
    public class LocalProfanityFilter : ILocalProfanityFilter
    {
        private readonly ConcurrentDictionary<string, byte> _dict = new(StringComparer.OrdinalIgnoreCase);
        // volatile is used to signal that this field may be updated by concurrently executing threads.
        private volatile Regex _regex;

        public void ReplaceDictionary(IEnumerable<string> words)
        {
            _dict.Clear();
            foreach(var w in words.Select(w => (w ?? string.Empty).Trim().ToLowerInvariant()).Where(w => w.Length > 0)) _dict[w] = 1;
            _regex = BuildRegex();
        }

        public string Sanitize(string content)
        {
            var rx = _regex ??= BuildRegex();
            if (rx is null) return content;
            return rx.Replace(content, m => new string('*', m.Value.Length));
        }

        private Regex? BuildRegex()
        {
            if (_dict.IsEmpty) return null;
            var escaped = _dict.Keys.Select(Regex.Escape);
            var pattern = $@"\b({string.Join("|", escaped)})\b";
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        }
    }
}
