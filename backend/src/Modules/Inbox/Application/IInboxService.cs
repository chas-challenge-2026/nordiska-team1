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
    Task<bool> CloseThreadAsync(long customerId, long threadId, bool isStaff, CancellationToken cancellationToken = default);
    Task<bool> ReopenThreadAsync(long customerId, long threadId, bool isStaff, CancellationToken cancellationToken = default);
    Task<MessageResponse?> AddStaffReplyAsync(long staffId, long threadId, StaffReplyRequest request, CancellationToken cancellationToken = default);

    // Admin Threads & Broadcast
    Task<PagedResult<AdminThreadSummaryResponse>> GetAdminThreadsAsync(AdminThreadQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<int> CreateAdminThreadAsync(CreateAdminThreadRequest request, CancellationToken cancellationToken = default);

    // Notifications
    Task<PagedResult<CustomerNotificationResponse>> GetNotificationsAsync(long customerId, NotificationQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<bool> MarkNotificationReadAsync(long customerId, long notificationId, CancellationToken cancellationToken = default);
    Task<int> MarkAllNotificationsReadAsync(long customerId, CancellationToken cancellationToken = default);

    // Digital Document Archive
    Task<PagedResult<DocumentResponse>> GetDocumentsAsync(long customerId, DocumentQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<DocumentDownloadResult?> DownloadDocumentAsync(long customerId, long documentId, CancellationToken cancellationToken = default);

    // Terms and Conditions Acceptance (NOR-254)
    Task<IReadOnlyList<PendingTermResponse>> GetPendingTermsAsync(long customerId, CancellationToken cancellationToken = default);
    Task<TermAcceptanceResult> AcceptTermAsync(long customerId, long termId, CancellationToken cancellationToken = default);
    Task<TermResponse> PublishTermAsync(PublishTermRequest request, CancellationToken cancellationToken = default);
}