namespace Nordiska.Modules.Documents.Domain;
public sealed class CustomerDocument
{
    public long Id { get; private set; }
    public long DocumentId { get; private set; }
    public long CustomerId { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }
    public DateTimeOffset? FirstOpenedAt { get; private set; }
    public DateTimeOffset? AvailableUntil { get; private set; }
    private CustomerDocument() { }
    public CustomerDocument(long documentId,long customerId,DateTimeOffset? availableUntil=null){DocumentId=documentId; CustomerId=customerId; PublishedAt=DateTimeOffset.UtcNow; AvailableUntil=availableUntil;}
    public bool HasBeenOpened=>FirstOpenedAt is not null;
    public void MarkOpened()=>FirstOpenedAt ??= DateTimeOffset.UtcNow;
    public bool IsAvailable(DateTimeOffset now)=>AvailableUntil is null || AvailableUntil>now;
}
