namespace Nordiska.Modules.Faq.Domain;

public sealed class FaqViewLog
{
    public long Id { get; private set; }
    public int FaqEntryId { get; private set; }
    public string SessionHash { get; private set; } = string.Empty;
    public DateTime ViewedAt { get; private set; }

    private FaqViewLog() { }

    public static FaqViewLog Create(int faqEntryId, string sessionHash, DateTime viewedAt)
    {
        return new FaqViewLog
        {
            FaqEntryId = faqEntryId,
            SessionHash = sessionHash,
            ViewedAt = viewedAt
        };
    }
}
