namespace Nordiska.Modules.Reporting.Domain;

public sealed class AuditEntry
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public long? UserId { get; set; }
    public string Details { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

}
