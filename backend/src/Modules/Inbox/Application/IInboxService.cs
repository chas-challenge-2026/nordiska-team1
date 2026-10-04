using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Inbox.Contracts.Requests;
using Nordiska.Modules.Inbox.Contracts.Responses;

namespace Nordiska.Modules.Inbox.Application;

public interface IInboxService
{
    Task<PagedResult<ThreadSummaryResponse>> GetThreadsAsync(long customerId, InboxQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<UnreadCountResponse> GetUnreadCountAsync(long customerId, CancellationToken cancellationToken = default);
    Task<ThreadDetailResponse?> GetThreadDetailsAsync(long customerId, long threadId, CancellationToken cancellationToken = default);
    Task<ThreadDetailResponse> CreateSupportTicketAsync(long customerId, CreateThreadRequest request, CancellationToken cancellationToken = default);
    Task<MessageResponse?> ReplyToThreadAsync(long customerId, long threadId, ReplyThreadRequest request, CancellationToken cancellationToken = default);
    Task<bool> MarkAsReadAsync(long customerId, long threadId, CancellationToken cancellationToken = default);
    Task<bool> ArchiveThreadAsync(long customerId, long threadId, CancellationToken cancellationToken = default);
    Task<bool> RestoreThreadAsync(long customerId, long threadId, CancellationToken cancellationToken = default);
    Task<MessageResponse?> AddStaffReplyAsync(long staffId, long threadId, StaffReplyRequest request, CancellationToken cancellationToken = default);
}

