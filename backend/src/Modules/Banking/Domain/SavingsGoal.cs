using System;

namespace Nordiska.Modules.Banking.Domain;

/// <summary>
/// Represents a targeted savings goal attached to a customer's savings account.
/// </summary>
public sealed class SavingsGoal
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public long CustomerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime? TargetDate { get; set; }
    public string Status { get; set; } = "active"; // active, paused, completed
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public SavingsAccount Account { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
}