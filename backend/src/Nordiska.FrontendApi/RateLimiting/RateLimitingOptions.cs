namespace Nordiska.FrontendApi.RateLimiting;

/// <summary>
/// Rate limiting settings bound from the "RateLimiting" section in appsettings.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public FixedWindowSettings Auth { get; set; } = new();
    public SlidingWindowSettings Transactions { get; set; } = new();
}

public sealed class FixedWindowSettings
{
    public int PermitLimit { get; set; } = 10;
    public int WindowSeconds { get; set; } = 60;
}

public sealed class SlidingWindowSettings
{
    public int PermitLimit { get; set; } = 10;
    public int WindowSeconds { get; set; } = 60;
    public int SegmentsPerWindow { get; set; } = 6;
}

/// <summary>
/// Policy names used with [EnableRateLimiting] on controller actions.
/// </summary>
public static class RateLimitPolicies
{
    public const string Auth = "auth";
    public const string Transactions = "transactions";
}
