using System;

namespace Nordiska.Modules.Banking.Domain;

public sealed class OperationalMessage
{
    public long Id { get; set; }
    public string TitleSv { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string MessageSv { get; set; } = string.Empty;
    public string MessageEn { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 0;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
