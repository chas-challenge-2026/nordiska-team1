using Nordiska.BuildingBlocks.Database;

namespace Nordiska.Modules.Inbox.Application;

public interface IInboxService
{
    Task<PagedResult<InboxThreadResponse>> GetThreadsAsync(
        long customerId,
        InboxThreadQuery query,
        CancellationToken cancellationToken = default);

    Task<InboxThreadResponse> GetThreadAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken = default);

    Task<InboxThreadResponse> CreateThreadAsync(
        long customerId,
        CreateInboxThreadRequest request,
        CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken = default);

    Task ArchiveAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken = default);
}