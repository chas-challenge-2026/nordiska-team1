namespace Nordiska.Modules.Agreements.Domain;
public sealed class Term
{
    public long Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public long DocumentId { get; private set; }
    public TermStatus Status { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
    private Term() { }
    public Term(string code,int version,string title,long documentId,DateTimeOffset effectiveFrom){Code=code; Version=version; Title=title; DocumentId=documentId; EffectiveFrom=effectiveFrom; Status=TermStatus.Published; PublishedAt=DateTimeOffset.UtcNow;}
    public void Retire()=>Status=TermStatus.Retired;
}
public enum TermStatus { Draft=1, Published=2, Retired=3 }
