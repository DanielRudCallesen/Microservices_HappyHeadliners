namespace CommentService.Interface
{
    public interface IProfanityClient
    {
        Task<(string SanitizedContent, bool HasProfanity)> FilterAsync(string content, CancellationToken ct);
        Task<string[]> GetDictionaryAsync(CancellationToken ct);
    }
}
