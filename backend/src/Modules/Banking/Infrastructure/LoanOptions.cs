namespace Nordiska.Modules.Banking.Infrastructure;

/// <summary>
/// Limits and pricing for customer loans. Rates are fractions (0.035 = 3.5 %).
/// </summary>
public sealed class LoanOptions
{
    public const string SectionName = "Loans";

    // Added on top of the Riksbank policy rate when a personal loan is created
    public decimal PersonalMargin { get; set; } = 0.05m;

    // Used instead of the policy rate if the Riksbank has never been reached
    public decimal FallbackBaseRate { get; set; } = 0.0175m;

    public decimal MinAmount { get; set; } = 10_000m;
    public decimal MaxAmount { get; set; } = 500_000m;
    public int MinTermMonths { get; set; } = 12;
    public int MaxTermMonths { get; set; } = 120;

    // Cap on the sum of all active personal loans per customer, mortgages are not counted
    public decimal MaxTotalPersonalDebt { get; set; } = 500_000m;
}
