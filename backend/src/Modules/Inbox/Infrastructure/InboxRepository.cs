using Microsoft.EntityFrameworkCore;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Communication.Domain;
using Nordiska.Modules.Inbox.Application;
using Nordiska.Modules.Inbox.Infrastructure.Db;

namespace Nordiska.Modules.Inbox.Infrastructure;

public sealed class InboxRepository(InboxDbContext dbContext) : IInboxRepository
{
    private readonly InboxDbContext _dbContext = dbContext;

    public async Task<MessageBox?> GetMessageBoxByCustomerIdAsync(long customerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MessageBoxes
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CustomerId == customerId, cancellationToken);
    }

    public async Task<MessageBox> EnsureMessageBoxAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.MessageBoxes
            .FirstOrDefaultAsync(m => m.CustomerId == customerId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var newBox = new MessageBox(customerId);
        _dbContext.MessageBoxes.Add(newBox);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return newBox;
    }

    public async Task<PagedResult<MessageThread>> GetThreadsByCustomerIdAsync(
        long customerId,
        MessageFolder folder,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = from thread in _dbContext.MessageThreads
                    join state in _dbContext.MessageThreadStates
                        on thread.Id equals state.ThreadId
                    where state.CustomerId == customerId && state.Folder == folder
                    orderby thread.LastMessageAt descending
                    select thread;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .AsNoTracking()
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<MessageThread>.Create(items, totalCount, page, pageSize);
    }

    public async Task<int> GetUnreadCountAsync(long customerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MessageThreadStates
            .AsNoTracking()
            .CountAsync(s => s.CustomerId == customerId && s.Folder == MessageFolder.Inbox && s.ReadAt == null, cancellationToken);
    }

    public async Task<MessageThread?> GetThreadByIdAsync(long threadId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MessageThreads
            .FirstOrDefaultAsync(t => t.Id == threadId, cancellationToken);
    }

    public async Task<MessageThreadState?> GetThreadStateAsync(long threadId, long customerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MessageThreadStates
            .FirstOrDefaultAsync(s => s.ThreadId == threadId && s.CustomerId == customerId, cancellationToken);
    }

    public async Task<IReadOnlyList<MessageThreadState>> GetAllThreadStatesAsync(long threadId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.MessageThreadStates
            .Where(s => s.ThreadId == threadId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> GetMessagesByThreadIdAsync(long threadId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Messages
            .AsNoTracking()
            .Where(m => m.ThreadId == threadId && m.RevokedAt == null)
            .OrderBy(m => m.SentAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<MessageThread> CreateThreadWithInitialMessageAsync(
        long customerId,
        string subject,
        string initialMessageBody,
        bool replyAllowed = true,
        CancellationToken cancellationToken = default)
    {
        var messageBox = await EnsureMessageBoxAsync(customerId, cancellationToken);

        var thread = new MessageThread(messageBox.Id, subject);
        _dbContext.MessageThreads.Add(thread);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var message = new Message(
            thread.Id,
            MessageSenderType.Customer,
            initialMessageBody,
            replyAllowed,
            senderCustomerId: customerId);
        _dbContext.Messages.Add(message);

        var threadState = new MessageThreadState(thread.Id, customerId, MessageFolder.Inbox);
        threadState.MarkAsRead();
        _dbContext.MessageThreadStates.Add(threadState);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return thread;
    }

    public async Task<Message> AddMessageAsync(
        long threadId,
        MessageSenderType senderType,
        long? senderCustomerId,
        string body,
        bool replyAllowed,
        CancellationToken cancellationToken = default)
    {
        var thread = await _dbContext.MessageThreads.FindAsync([threadId], cancellationToken)
            ?? throw new InvalidOperationException($"Thread {threadId} not found.");

        var message = new Message(
            threadId,
            senderType,
            body,
            replyAllowed,
            senderCustomerId);

        _dbContext.Messages.Add(message);
        thread.RegisterMessage(DateTimeOffset.UtcNow);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task MarkAsReadAsync(long threadId, long customerId, CancellationToken cancellationToken = default)
    {
        var state = await _dbContext.MessageThreadStates
            .FirstOrDefaultAsync(s => s.ThreadId == threadId && s.CustomerId == customerId, cancellationToken);

        if (state is not null && !state.IsRead)
        {
            state.MarkAsRead();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsUnreadAsync(long threadId, long customerId, CancellationToken cancellationToken = default)
    {
        var state = await _dbContext.MessageThreadStates
            .FirstOrDefaultAsync(s => s.ThreadId == threadId && s.CustomerId == customerId, cancellationToken);

        if (state is not null && state.IsRead)
        {
            state.MarkAsUnread();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ArchiveThreadAsync(long threadId, long customerId, CancellationToken cancellationToken = default)
    {
        var state = await _dbContext.MessageThreadStates
            .FirstOrDefaultAsync(s => s.ThreadId == threadId && s.CustomerId == customerId, cancellationToken);

        if (state is not null && state.Folder != MessageFolder.Archive)
        {
            state.Archive();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MoveToInboxAsync(long threadId, long customerId, CancellationToken cancellationToken = default)
    {
        var state = await _dbContext.MessageThreadStates
            .FirstOrDefaultAsync(s => s.ThreadId == threadId && s.CustomerId == customerId, cancellationToken);

        if (state is not null && state.Folder != MessageFolder.Inbox)
        {
            state.MoveToInbox();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<CustomerNotification> AddNotificationAsync(
        long customerId,
        string type,
        string title,
        string? body = null,
        NotificationPriority priority = NotificationPriority.Normal,
        NotificationTargetType? targetType = null,
        long? targetId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new CustomerNotification(
            customerId,
            type,
            title,
            body,
            priority,
            targetType,
            targetId);

        _dbContext.CustomerNotifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return notification;
    }
}
