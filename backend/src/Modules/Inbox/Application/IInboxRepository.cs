using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Communication.Domain;
using Nordiska.Modules.Documents.Domain;

namespace Nordiska.Modules.Inbox.Application;

public interface IInboxRepository
{
    Task<MessageBox?> GetMessageBoxByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default);
    Task<MessageBox> EnsureMessageBoxAsync(long customerId, CancellationToken cancellationToken = default);
    Task<PagedResult<MessageThread>> GetThreadsByCustomerIdAsync(long customerId, MessageFolder folder, int page, int pageSize, CancellationToken cancellationToken = default);
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
    Task<CustomerNotification> AddNotificationAsync(
        long customerId,
        string type,
        string title,
        string? body = null,
        NotificationPriority priority = NotificationPriority.Normal,
        NotificationTargetType? targetType = null,
        long? targetId = null,
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
}