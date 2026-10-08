namespace Nordiska.Modules.Inbox.Application;

public sealed record FeedReadCounts(
    int Threads,
    int Notifications,
    int Documents,
    int Terms)
{
    public int Total => Threads + Notifications + Documents + Terms;
}
