namespace Nordiska.Modules.Banking.Infrastructure.External.Riksbank;

public sealed class RiksbankOptions
{
    public const string SectionName = "Riksbank";

    // Needs the trailing slash, otherwise HttpClient drops "v1" when it combines the base address with the relative path
    public string BaseUrl { get; set; } = "https://api.riksbank.se/swea/v1/";

    // SECBREPOEFF is the policy rate series in SWEA
    public string SeriesId { get; set; } = "SECBREPOEFF";

    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(6);
}
