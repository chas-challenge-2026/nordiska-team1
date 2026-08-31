namespace Bank.Domain.Entities;

public sealed class Notification
{
    public long Id { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long RefId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
