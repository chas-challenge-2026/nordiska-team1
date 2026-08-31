namespace Bank.Modules.Faq.Domain;

public sealed class FaqEntry
{
    public int Id { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int HelpfulCount { get; set; }
    public string Keywords { get; set; } = string.Empty;
}
