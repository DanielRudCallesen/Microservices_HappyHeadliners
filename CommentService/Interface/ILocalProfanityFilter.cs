namespace CommentService.Interface
{
    public interface ILocalProfanityFilter
    {
        string Sanitize(string content);
        void ReplaceDictionary(IEnumerable<string> words);
    }
}
