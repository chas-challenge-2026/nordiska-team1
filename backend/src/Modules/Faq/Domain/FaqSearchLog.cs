namespace Nordiska.Modules.Faq.Domain;

public sealed class FaqSearchLog
{
    public long Id { get; private set; }
    public string Query { get; private set; } = string.Empty;
    public string NormalizedQuery { get; private set; } = string.Empty;
    public string Language { get; private set; } = "sv";
    public int ResultCount { get; private set; }
    public string SessionHash { get; private set; } = string.Empty;
    public DateTime SearchedAt { get; private set; }

    private FaqSearchLog() { }

    public static FaqSearchLog Create(
        string query,
        string normalizedQuery,
        string language,
        int resultCount,
        string sessionHash,
        DateTime searchedAt)
    {
        return new FaqSearchLog
        {
            Query = query,
            NormalizedQuery = normalizedQuery,
            Language = language,
            ResultCount = resultCount,
            SessionHash = sessionHash,
            SearchedAt = searchedAt
        };
    }
}
