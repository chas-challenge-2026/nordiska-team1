namespace Nordiska.Modules.Agreements.Domain;
public sealed class TermAcceptance
{
    public long Id { get; private set; }
    public long TermId { get; private set; }
    public long CustomerId { get; private set; }
    public TermAcceptanceStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? DeclinedAt { get; private set; }
    private TermAcceptance() { }
    public TermAcceptance(long termId,long customerId){TermId=termId; CustomerId=customerId; Status=TermAcceptanceStatus.Pending; CreatedAt=DateTimeOffset.UtcNow;}
    public void Accept(){EnsurePending(); Status=TermAcceptanceStatus.Accepted; AcceptedAt=DateTimeOffset.UtcNow;}
    public void Decline(){EnsurePending(); Status=TermAcceptanceStatus.Declined; DeclinedAt=DateTimeOffset.UtcNow;}
    private void EnsurePending(){if(Status!=TermAcceptanceStatus.Pending) throw new InvalidOperationException("Response already recorded.");}
}
public enum TermAcceptanceStatus { Pending=1, Accepted=2, Declined=3 }
