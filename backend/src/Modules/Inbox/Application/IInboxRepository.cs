using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Agreements.Domain;
using Nordiska.Modules.Communication.Domain;
using Nordiska.Modules.CustomerCenter.Domain;
using Nordiska.Modules.Documents.Domain;
using Nordiska.Modules.Inbox.Contracts.Requests;
using Nordiska.Modules.Inbox.Contracts.Responses;

namespace Nordiska.Modules.Inbox.Application;

public interface IInboxRepository
{
    Task<MessageBox?> GetMessageBoxByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default);
    Task<MessageBox> EnsureMessageBoxAsync(long customerId, CancellationToken cancellationToken = default);
    Task<PagedResult<MessageThread>> GetThreadsByCustomerIdAsync(
        long customerId,
        MessageFolder folder,
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(long customerId, CancellationToken cancellationToken = default);
    Task<MessageThread?> GetThreadByIdAsync(long threadId, CancellationToken cancellationToken = default);
    Task<MessageThreadState?> GetThreadStateAsync(long threadId, long customerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageThreadState>> GetAllThreadStatesAsync(long threadId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Message>> GetMessagesByThreadIdAsync(long threadId, CancellationToken cancellationToken = default);
    Task<MessageThread> CreateThreadWithInitialMessageAsync(
        long customerId,
        string subject,
        string initialMessageBody,
        bool replyAllowed = true,
        bool isInformationOnly = false,
        string? category = null,
        CancellationToken cancellationToken = default);
    Task<MessageThread> CreateThreadWithInitialMessageAsync(
        long customerId,
        string subject,
        string initialMessageBody,
        bool replyAllowed,
        bool isInformationOnly,
        string? category,
        MessageSenderType senderType,
        CancellationToken cancellationToken = default);
    Task<Message> AddMessageAsync(
        long threadId,
        MessageSenderType senderType,
        long? senderCustomerId,
        string body,
        bool replyAllowed,
        CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(long threadId, long customerId, CancellationToken cancellationToken = default);
    Task MarkAsUnreadAsync(long threadId, long customerId, CancellationToken cancellationToken = default);
    Task ArchiveThreadAsync(long threadId, long customerId, CancellationToken cancellationToken = default);
    Task MoveToInboxAsync(long threadId, long customerId, CancellationToken cancellationToken = default);
    Task<bool> CloseThreadAsync(long threadId, CancellationToken cancellationToken = default);
    Task<bool> ReopenThreadAsync(long threadId, CancellationToken cancellationToken = default);

    // Admin & Broadcast
    Task<PagedResult<(MessageThread Thread, long CustomerId, int MessageCount)>> GetAdminThreadsAsync(
        AdminThreadQueryParameters parameters,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<long>> GetAllCustomerIdsAsync(CancellationToken cancellationToken = default);

    // Notifications
    Task<CustomerNotification> AddNotificationAsync(
        long customerId,
        string type,
        string title,
        string? body = null,
        NotificationPriority priority = NotificationPriority.Normal,
        NotificationTargetType? targetType = null,
        long? targetId = null,
        CancellationToken cancellationToken = default);
    Task<PagedResult<CustomerNotification>> GetCustomerNotificationsAsync(
        long customerId,
        bool unreadOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> MarkNotificationReadAsync(
        long customerId,
        long notificationId,
        CancellationToken cancellationToken = default);
    Task<int> MarkAllNotificationsReadAsync(
        long customerId,
        CancellationToken cancellationToken = default);

    // Digital Document Archive
    Task<PagedResult<(CustomerDocument CustomerDoc, Document Doc)>> GetCustomerDocumentsAsync(
        long customerId,
        int? year,
        string? documentType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(CustomerDocument? CustomerDoc, Document? Doc)> GetCustomerDocumentByIdAsync(
        long customerId,
        long documentId,
        CancellationToken cancellationToken = default);

    Task MarkDocumentOpenedAsync(long customerDocumentId, CancellationToken cancellationToken = default);

    // Terms and Conditions Acceptance (NOR-254)
    Task<IReadOnlyList<(TermAcceptance Acceptance, Term Term)>> GetPendingTermsAsync(
        long customerId,
        CancellationToken cancellationToken = default);

    Task<(TermAcceptance? Acceptance, Term? Term)> GetTermAcceptanceAsync(
        long customerId,
        long termId,
        CancellationToken cancellationToken = default);

    Task<TermAcceptance> AcceptTermAsync(
        TermAcceptance acceptance,
        CancellationToken cancellationToken = default);

    Task<Term> PublishTermAsync(
        string code,
        int version,
        string title,
        long documentId,
        DateTimeOffset effectiveFrom,
        IEnumerable<long>? targetCustomerIds = null,
        CancellationToken cancellationToken = default);

    // Summary Counts, Feed & Global Mark Read
    Task<InboxSummaryCounts> GetSummaryCountsAsync(long customerId, CancellationToken cancellationToken = default);
    Task<PagedResult<InboxFeedItemResponse>> GetUnifiedFeedAsync(long customerId, FeedQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<int> MarkAllThreadsReadAsync(long customerId, CancellationToken cancellationToken = default);
    Task<bool> MarkFeedItemReadAsync(long customerId, FeedItemType itemType, long sourceId, CancellationToken cancellationToken = default);
    Task<FeedReadCounts> MarkAllFeedItemsReadAsync(long customerId, CancellationToken cancellationToken = default);

    // General Documents & Auto-Archiving
    Task<IReadOnlyList<Document>> GetGeneralDocumentsAsync(CancellationToken cancellationToken = default);
    Task<int> AutoArchiveOldReadThreadsAsync(long customerId, CancellationToken cancellationToken = default);
}
