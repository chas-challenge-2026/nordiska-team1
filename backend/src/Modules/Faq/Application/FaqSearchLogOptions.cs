namespace Nordiska.Modules.Faq.Application;

/// <summary>
/// Settings for logging FAQ searches and cleaning up old search logs.
/// </summary>
public sealed class FaqSearchLogOptions
{
    public const string SectionName = "FaqSearchLog";

    // Mixed into the session hash so the IP can't be brute-forced back out of it
    public string Salt { get; set; } = string.Empty;

    public int RetentionDays { get; set; } = 90;

    // Searches are dropped (not waited on) when this many are already waiting to be saved
    public int QueueCapacity { get; set; } = 1000;

    public int CleanupIntervalHours { get; set; } = 24;
}
