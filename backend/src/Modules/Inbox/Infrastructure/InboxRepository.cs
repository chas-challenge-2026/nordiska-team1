using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Agreements.Domain;
using Nordiska.Modules.Communication.Domain;
using Nordiska.Modules.CustomerCenter.Domain;
using Nordiska.Modules.Documents.Domain;
using Nordiska.Modules.Inbox.Application;
using Nordiska.Modules.Inbox.Contracts.Requests;
using Nordiska.Modules.Inbox.Contracts.Responses;
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
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<MessageThread> query;

        if (folder == MessageFolder.Sent)
        {
            query = from thread in _dbContext.MessageThreads
                    join state in _dbContext.MessageThreadStates on thread.Id equals state.ThreadId
                    where state.CustomerId == customerId &&
                          state.Folder != MessageFolder.Archive &&
                          _dbContext.Messages.Any(m => m.ThreadId == thread.Id && m.SenderCustomerId == customerId)
                    select thread;
        }
        else
        {
            query = from thread in _dbContext.MessageThreads
                    join state in _dbContext.MessageThreadStates on thread.Id equals state.ThreadId
                    where state.CustomerId == customerId && state.Folder == folder
                    select thread;
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(t => t.Subject.ToLower().Contains(term) ||
                _dbContext.Messages.Any(m => m.ThreadId == t.Id && m.Body.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .AsNoTracking()
            .OrderByDescending(t => t.LastMessageAt)
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

    public Task<MessageThread> CreateThreadWithInitialMessageAsync(
        long customerId,
        string subject,
        string initialMessageBody,
        bool replyAllowed = true,
        bool isInformationOnly = false,
        string? category = null,
        CancellationToken cancellationToken = default)
        => CreateThreadWithInitialMessageAsync(
            customerId,
            subject,
            initialMessageBody,
            replyAllowed,
            isInformationOnly,
            category,
            MessageSenderType.Customer,
            cancellationToken);

    public async Task<MessageThread> CreateThreadWithInitialMessageAsync(
        long customerId,
        string subject,
        string initialMessageBody,
        bool replyAllowed,
        bool isInformationOnly,
        string? category,
        MessageSenderType senderType,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var messageBox = await EnsureMessageBoxAsync(customerId, cancellationToken);

        var thread = new MessageThread(messageBox.Id, subject, isInformationOnly, category);
        _dbContext.MessageThreads.Add(thread);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var message = new Message(
            thread.Id,
            senderType,
            initialMessageBody,
            replyAllowed,
            senderCustomerId: senderType == MessageSenderType.Customer ? customerId : null);
        _dbContext.Messages.Add(message);

        var threadState = new MessageThreadState(thread.Id, customerId, MessageFolder.Inbox);
        if (senderType == MessageSenderType.Customer)
        {
            threadState.MarkAsRead();
        }
        else
        {
            threadState.MarkAsUnread();
        }
        _dbContext.MessageThreadStates.Add(threadState);

        var feedItem = new FeedItem(
            customerId,
            FeedItemType.Message,
            thread.Id,
            thread.Subject,
            Truncate(initialMessageBody, 1_000),
            FeedPriority.Normal,
            actionRequired: false,
            thread.LastMessageAt);
        if (senderType == MessageSenderType.Customer)
        {
            feedItem.MarkAsRead();
        }
        _dbContext.FeedItems.Add(feedItem);

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
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
        thread.RegisterMessage(message.SentAt);

        var states = await _dbContext.MessageThreadStates
            .Where(s => s.ThreadId == threadId)
            .ToListAsync(cancellationToken);
        foreach (var state in states)
        {
            var feedItem = await UpsertFeedItemAsync(
                state.CustomerId,
                FeedItemType.Message,
                thread.Id,
                thread.Subject,
                Truncate(body, 1_000),
                FeedPriority.Normal,
                actionRequired: false,
                message.SentAt,
                cancellationToken);

            if (senderType == MessageSenderType.Customer && senderCustomerId == state.CustomerId)
            {
                feedItem.MarkAsRead();
            }
            else
            {
                feedItem.MarkAsUnread();
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return message;
    }

    public async Task MarkAsReadAsync(long threadId, long customerId, CancellationToken cancellationToken = default)
    {
        var state = await _dbContext.MessageThreadStates
            .FirstOrDefaultAsync(s => s.ThreadId == threadId && s.CustomerId == customerId, cancellationToken);

        if (state is not null)
        {
            if (!state.IsRead)
            {
                state.MarkAsRead();
            }

            var feedItem = await _dbContext.FeedItems.FirstOrDefaultAsync(
                x => x.CustomerId == customerId && x.ItemType == FeedItemType.Message && x.SourceId == threadId,
                cancellationToken);
            feedItem?.MarkAsRead();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAsUnreadAsync(long threadId, long customerId, CancellationToken cancellationToken = default)
    {
        var state = await _dbContext.MessageThreadStates
            .FirstOrDefaultAsync(s => s.ThreadId == threadId && s.CustomerId == customerId, cancellationToken);

        if (state is not null)
        {
            if (state.IsRead)
            {
                state.MarkAsUnread();
            }

            var feedItem = await _dbContext.FeedItems.FirstOrDefaultAsync(
                x => x.CustomerId == customerId && x.ItemType == FeedItemType.Message && x.SourceId == threadId,
                cancellationToken);
            feedItem?.MarkAsUnread();
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

    public async Task<bool> CloseThreadAsync(long threadId, CancellationToken cancellationToken = default)
    {
        var thread = await _dbContext.MessageThreads.FindAsync([threadId], cancellationToken);
        if (thread is null)
        {
            return false;
        }

        thread.Close();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ReopenThreadAsync(long threadId, CancellationToken cancellationToken = default)
    {
        var thread = await _dbContext.MessageThreads.FindAsync([threadId], cancellationToken);
        if (thread is null)
        {
            return false;
        }

        thread.Reopen();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PagedResult<(MessageThread Thread, long CustomerId, int MessageCount)>> GetAdminThreadsAsync(
        AdminThreadQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, parameters.Page);
        var pageSize = Math.Clamp(parameters.PageSize, 1, 100);

        var query = from thread in _dbContext.MessageThreads
                    join box in _dbContext.MessageBoxes on thread.MessageBoxId equals box.Id
                    select new { Thread = thread, CustomerId = box.CustomerId };

        if (parameters.CustomerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == parameters.CustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Status))
        {
            if (Enum.TryParse<MessageThreadStatus>(parameters.Status, true, out var status))
            {
                query = query.Where(x => x.Thread.Status == status);
            }
        }

        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var term = parameters.SearchTerm.Trim().ToLower();
            query = query.Where(x => x.Thread.Subject.ToLower().Contains(term) ||
                _dbContext.Messages.Any(m => m.ThreadId == x.Thread.Id && m.Body.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Thread.LastMessageAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Thread,
                x.CustomerId,
                MessageCount = _dbContext.Messages.Count(m => m.ThreadId == x.Thread.Id)
            })
            .ToListAsync(cancellationToken);

        var tupleItems = items.Select(x => (x.Thread, x.CustomerId, x.MessageCount)).ToList();

        return PagedResult<(MessageThread Thread, long CustomerId, int MessageCount)>.Create(
            tupleItems,
            totalCount,
            page,
            pageSize);
    }

    public async Task<IReadOnlyList<long>> GetAllCustomerIdsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.MessageBoxes
            .AsNoTracking()
            .Select(m => m.CustomerId)
            .Distinct()
            .ToListAsync(cancellationToken);
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
        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

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

        if (targetType != NotificationTargetType.MessageThread)
        {
            var feedPriority = priority switch
            {
                NotificationPriority.Critical => FeedPriority.Critical,
                NotificationPriority.High => FeedPriority.Important,
                _ => FeedPriority.Normal
            };
            await UpsertFeedItemAsync(
                customerId,
                FeedItemType.Notification,
                notification.Id,
                title,
                Truncate(body, 1_000),
                feedPriority,
                actionRequired: priority == NotificationPriority.Critical,
                notification.CreatedAt,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return notification;
    }

    public async Task<PagedResult<CustomerNotification>> GetCustomerNotificationsAsync(
        long customerId,
        bool unreadOnly,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.CustomerNotifications
            .AsNoTracking()
            .Where(n => n.CustomerId == customerId);

        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<CustomerNotification>.Create(items, totalCount, page, pageSize);
    }

    public async Task<bool> MarkNotificationReadAsync(
        long customerId,
        long notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.CustomerNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.CustomerId == customerId, cancellationToken);

        if (notification is null)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.MarkAsRead();
        }

        var feedItem = await _dbContext.FeedItems.FirstOrDefaultAsync(
            x => x.CustomerId == customerId && x.ItemType == FeedItemType.Notification && x.SourceId == notificationId,
            cancellationToken);
        feedItem?.MarkAsRead();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<int> MarkAllNotificationsReadAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var unread = await _dbContext.CustomerNotifications
            .Where(n => n.CustomerId == customerId && n.ReadAt == null)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
        {
            return 0;
        }

        foreach (var n in unread)
        {
            n.MarkAsRead();
        }

        var notificationIds = unread.Select(n => n.Id).ToList();
        var feedItems = await _dbContext.FeedItems
            .Where(x => x.CustomerId == customerId && x.ItemType == FeedItemType.Notification && notificationIds.Contains(x.SourceId))
            .ToListAsync(cancellationToken);
        foreach (var feedItem in feedItems)
        {
            feedItem.MarkAsRead();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }

    public async Task<PagedResult<(CustomerDocument CustomerDoc, Document Doc)>> GetCustomerDocumentsAsync(
        long customerId,
        int? year,
        string? documentType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = from cd in _dbContext.CustomerDocuments.AsNoTracking()
                    join d in _dbContext.Documents.AsNoTracking() on cd.DocumentId equals d.Id
                    where cd.CustomerId == customerId && d.Status != DocumentStatus.Deleted
                    select new { CustomerDoc = cd, Doc = d };

        if (!string.IsNullOrWhiteSpace(documentType))
        {
            query = query.Where(x => x.Doc.DocumentType == documentType);
        }

        if (year.HasValue)
        {
            query = query.Where(x => x.CustomerDoc.PublishedAt.Year == year.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CustomerDoc.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var tupleItems = items.Select(x => (x.CustomerDoc, x.Doc)).ToList();

        return PagedResult<(CustomerDocument CustomerDoc, Document Doc)>.Create(
            tupleItems,
            totalCount,
            page,
            pageSize);
    }

    public async Task<(CustomerDocument? CustomerDoc, Document? Doc)> GetCustomerDocumentByIdAsync(
        long customerId,
        long documentId,
        CancellationToken cancellationToken = default)
    {
        var result = await (from cd in _dbContext.CustomerDocuments
                            join d in _dbContext.Documents on cd.DocumentId equals d.Id
                            where cd.CustomerId == customerId && (cd.DocumentId == documentId || cd.Id == documentId) && d.Status != DocumentStatus.Deleted
                            select new { CustomerDoc = cd, Doc = d })
                            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
        {
            return (null, null);
        }

        return (result.CustomerDoc, result.Doc);
    }

    public async Task MarkDocumentOpenedAsync(long customerDocumentId, CancellationToken cancellationToken = default)
    {
        var customerDoc = await _dbContext.CustomerDocuments.FindAsync([customerDocumentId], cancellationToken);
        if (customerDoc is not null && !customerDoc.HasBeenOpened)
        {
            customerDoc.MarkOpened();
        }

        if (customerDoc is not null)
        {
            var feedItem = await _dbContext.FeedItems.FirstOrDefaultAsync(
                x => x.CustomerId == customerDoc.CustomerId && x.ItemType == FeedItemType.Document && x.SourceId == customerDoc.DocumentId,
                cancellationToken);
            feedItem?.MarkAsRead();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<(TermAcceptance Acceptance, Term Term)>> GetPendingTermsAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var query = from ta in _dbContext.TermAcceptances
                    join t in _dbContext.Terms on ta.TermId equals t.Id
                    where ta.CustomerId == customerId && ta.Status == TermAcceptanceStatus.Pending && t.Status == TermStatus.Published
                    orderby t.EffectiveFrom descending
                    select new { Acceptance = ta, Term = t };

        var list = await query.ToListAsync(cancellationToken);
        return list.Select(x => (x.Acceptance, x.Term)).ToList();
    }

    public async Task<(TermAcceptance? Acceptance, Term? Term)> GetTermAcceptanceAsync(
        long customerId,
        long termId,
        CancellationToken cancellationToken = default)
    {
        var query = from ta in _dbContext.TermAcceptances
                    join t in _dbContext.Terms on ta.TermId equals t.Id
                    where ta.CustomerId == customerId && (ta.TermId == termId || ta.Id == termId)
                    select new { Acceptance = ta, Term = t };

        var result = await query.FirstOrDefaultAsync(cancellationToken);
        return result is null ? (null, null) : (result.Acceptance, result.Term);
    }

    public async Task<TermAcceptance> AcceptTermAsync(
        TermAcceptance acceptance,
        CancellationToken cancellationToken = default)
    {
        acceptance.Accept();
        _dbContext.TermAcceptances.Update(acceptance);

        var termVersion = await _dbContext.Terms
            .Where(x => x.Id == acceptance.TermId)
            .Select(x => x.Version)
            .SingleAsync(cancellationToken);

        var feedItem = await _dbContext.FeedItems.FirstOrDefaultAsync(
            x => x.CustomerId == acceptance.CustomerId && x.ItemType == FeedItemType.Terms && x.SourceId == acceptance.TermId,
            cancellationToken);
        if (feedItem is not null)
        {
            feedItem.Update(
                feedItem.Title,
                $"Version {termVersion}. Godkänd.",
                FeedPriority.Normal,
                actionRequired: false,
                feedItem.OccurredAt);
            feedItem.MarkAsRead();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return acceptance;
    }

    public async Task<Term> PublishTermAsync(
        string code,
        int version,
        string title,
        long documentId,
        DateTimeOffset effectiveFrom,
        IEnumerable<long>? targetCustomerIds = null,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var term = new Term(code, version, title, documentId, effectiveFrom);
        _dbContext.Terms.Add(term);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var customerIds = targetCustomerIds?.ToList();
        if (customerIds is null || customerIds.Count == 0)
        {
            customerIds = await _dbContext.MessageBoxes
                .Select(mb => mb.CustomerId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        var acceptances = customerIds.Select(cid => new TermAcceptance(term.Id, cid)).ToList();
        if (acceptances.Count > 0)
        {
            _dbContext.TermAcceptances.AddRange(acceptances);
            foreach (var acceptance in acceptances)
            {
                _dbContext.FeedItems.Add(new FeedItem(
                    acceptance.CustomerId,
                    FeedItemType.Terms,
                    term.Id,
                    term.Title,
                    $"Version {term.Version}. Inväntar digital acceptans.",
                    FeedPriority.Important,
                    actionRequired: true,
                    term.PublishedAt));
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return term;
    }

    public async Task<InboxSummaryCounts> GetSummaryCountsAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var unreadFeed = _dbContext.FeedItems
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.ReadAt == null);

        var unreadThreads = await unreadFeed.CountAsync(x => x.ItemType == FeedItemType.Message, cancellationToken);
        var unreadNotifications = await unreadFeed.CountAsync(x => x.ItemType == FeedItemType.Notification, cancellationToken);
        var unreadDocuments = await unreadFeed.CountAsync(x => x.ItemType == FeedItemType.Document, cancellationToken);
        var unreadTerms = await unreadFeed.CountAsync(x => x.ItemType == FeedItemType.Terms, cancellationToken);
        var actionRequired = await _dbContext.FeedItems
            .AsNoTracking()
            .CountAsync(x => x.CustomerId == customerId && x.ActionRequired, cancellationToken);

        var unopenedDocuments = await (from cd in _dbContext.CustomerDocuments
                                       join d in _dbContext.Documents on cd.DocumentId equals d.Id
                                       where cd.CustomerId == customerId && cd.FirstOpenedAt == null && d.Status != DocumentStatus.Deleted
                                       select cd)
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var pendingTerms = await (from a in _dbContext.TermAcceptances
                                  join t in _dbContext.Terms on a.TermId equals t.Id
                                  where a.CustomerId == customerId && a.Status == TermAcceptanceStatus.Pending && t.Status == TermStatus.Published
                                  select a)
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var totalUnread = unreadThreads + unreadNotifications + unreadDocuments + unreadTerms;

        return new InboxSummaryCounts(
            TotalUnread: totalUnread,
            UnreadThreads: unreadThreads,
            UnreadNotifications: unreadNotifications,
            UnopenedDocuments: unopenedDocuments,
            PendingTerms: pendingTerms,
            UnreadDocuments: unreadDocuments,
            UnreadTerms: unreadTerms,
            ActionRequired: actionRequired);
    }

    public async Task<PagedResult<InboxFeedItemResponse>> GetUnifiedFeedAsync(
        long customerId,
        FeedQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var filterType = ParseFeedItemTypeFilter(parameters.Type);
        var page = parameters.Page < 1 ? 1 : parameters.Page;
        var pageSize = parameters.PageSize is < 1 or > 100 ? 20 : parameters.PageSize;

        var supportedTypes = new[]
        {
            FeedItemType.Message,
            FeedItemType.Notification,
            FeedItemType.Document,
            FeedItemType.Terms
        };
        var query = _dbContext.FeedItems
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId && supportedTypes.Contains(x.ItemType));

        if (filterType.HasValue)
        {
            query = query.Where(x => x.ItemType == filterType.Value);
        }

        if (parameters.UnreadOnly)
        {
            query = query.Where(x => x.ReadAt == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var feedItems = await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenBy(x => x.ItemType)
            .ThenByDescending(x => x.SourceId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var threadIds = feedItems.Where(x => x.ItemType == FeedItemType.Message).Select(x => x.SourceId).ToList();
        var notificationIds = feedItems.Where(x => x.ItemType == FeedItemType.Notification).Select(x => x.SourceId).ToList();
        var documentIds = feedItems.Where(x => x.ItemType == FeedItemType.Document).Select(x => x.SourceId).ToList();

        var threadCategories = await _dbContext.MessageThreads
            .AsNoTracking()
            .Where(x => threadIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Category, cancellationToken);
        var notificationMetadata = await _dbContext.CustomerNotifications
            .AsNoTracking()
            .Where(x => notificationIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Type, x.TargetType, x.TargetId })
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var documentTypes = await _dbContext.Documents
            .AsNoTracking()
            .Where(x => documentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DocumentType, cancellationToken);

        var pagedItems = feedItems.Select(item =>
        {
            var type = MapFeedItemType(item.ItemType);
            string? category = item.ItemType switch
            {
                FeedItemType.Message => threadCategories.GetValueOrDefault(item.SourceId) ?? "Meddelande",
                FeedItemType.Notification => notificationMetadata.GetValueOrDefault(item.SourceId)?.Type,
                FeedItemType.Document => documentTypes.GetValueOrDefault(item.SourceId),
                FeedItemType.Terms => "Villkor",
                _ => null
            };
            string? targetUrl = item.ItemType switch
            {
                FeedItemType.Message => $"/api/inbox/threads/{item.SourceId}",
                FeedItemType.Notification => notificationMetadata.GetValueOrDefault(item.SourceId) is { } notification && notification.TargetId.HasValue
                    ? notification.TargetType switch
                    {
                        NotificationTargetType.MessageThread => $"/api/inbox/threads/{notification.TargetId.Value}",
                        NotificationTargetType.Document => $"/api/inbox/documents/{notification.TargetId.Value}/download",
                        NotificationTargetType.Term => $"/api/inbox/terms/{notification.TargetId.Value}/accept",
                        _ => null
                    }
                    : null,
                FeedItemType.Document => $"/api/inbox/documents/{item.SourceId}/download",
                FeedItemType.Terms => $"/api/inbox/terms/{item.SourceId}/accept",
                _ => null
            };

            return new InboxFeedItemResponse(
                Id: FeedItemIdentity.Format(item.ItemType, item.SourceId),
                Type: type,
                SourceId: item.SourceId,
                Title: item.Title,
                Preview: item.Preview,
                Category: category,
                Priority: item.Priority.ToString(),
                IsRead: item.IsRead,
                ActionRequired: item.ActionRequired,
                OccurredAt: item.OccurredAt,
                TargetUrl: targetUrl);
        }).ToList();

        return PagedResult<InboxFeedItemResponse>.Create(pagedItems, totalCount, page, pageSize);
    }

    public async Task<int> MarkAllThreadsReadAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var unreadStates = await _dbContext.MessageThreadStates
            .Where(s => s.CustomerId == customerId && s.Folder == MessageFolder.Inbox && s.ReadAt == null)
            .ToListAsync(cancellationToken);

        if (unreadStates.Count == 0)
        {
            return 0;
        }

        foreach (var state in unreadStates)
        {
            state.MarkAsRead();
        }

        var threadIds = unreadStates.Select(s => s.ThreadId).ToList();
        var feedItems = await _dbContext.FeedItems
            .Where(x => x.CustomerId == customerId && x.ItemType == FeedItemType.Message && threadIds.Contains(x.SourceId))
            .ToListAsync(cancellationToken);
        foreach (var feedItem in feedItems)
        {
            feedItem.MarkAsRead();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return unreadStates.Count;
    }

    public async Task<bool> MarkFeedItemReadAsync(
        long customerId,
        FeedItemType itemType,
        long sourceId,
        CancellationToken cancellationToken = default)
    {
        var feedItem = await _dbContext.FeedItems.FirstOrDefaultAsync(
            x => x.CustomerId == customerId && x.ItemType == itemType && x.SourceId == sourceId,
            cancellationToken);
        if (feedItem is null)
        {
            return false;
        }

        feedItem.MarkAsRead();

        if (itemType == FeedItemType.Message)
        {
            var state = await _dbContext.MessageThreadStates.FirstOrDefaultAsync(
                x => x.CustomerId == customerId && x.ThreadId == sourceId,
                cancellationToken);
            state?.MarkAsRead();
        }
        else if (itemType == FeedItemType.Notification)
        {
            var notification = await _dbContext.CustomerNotifications.FirstOrDefaultAsync(
                x => x.CustomerId == customerId && x.Id == sourceId,
                cancellationToken);
            notification?.MarkAsRead();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<FeedReadCounts> MarkAllFeedItemsReadAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var unreadItems = await _dbContext.FeedItems
            .Where(x => x.CustomerId == customerId &&
                        x.ReadAt == null &&
                        (x.ItemType == FeedItemType.Message ||
                         x.ItemType == FeedItemType.Notification ||
                         x.ItemType == FeedItemType.Document ||
                         x.ItemType == FeedItemType.Terms))
            .ToListAsync(cancellationToken);

        var counts = new FeedReadCounts(
            Threads: unreadItems.Count(x => x.ItemType == FeedItemType.Message),
            Notifications: unreadItems.Count(x => x.ItemType == FeedItemType.Notification),
            Documents: unreadItems.Count(x => x.ItemType == FeedItemType.Document),
            Terms: unreadItems.Count(x => x.ItemType == FeedItemType.Terms));

        if (unreadItems.Count == 0)
        {
            return counts;
        }

        foreach (var item in unreadItems)
        {
            item.MarkAsRead();
        }

        var threadIds = unreadItems
            .Where(x => x.ItemType == FeedItemType.Message)
            .Select(x => x.SourceId)
            .ToList();
        var notificationIds = unreadItems
            .Where(x => x.ItemType == FeedItemType.Notification)
            .Select(x => x.SourceId)
            .ToList();

        var threadStates = await _dbContext.MessageThreadStates
            .Where(x => x.CustomerId == customerId && threadIds.Contains(x.ThreadId))
            .ToListAsync(cancellationToken);
        foreach (var state in threadStates)
        {
            state.MarkAsRead();
        }

        var notifications = await _dbContext.CustomerNotifications
            .Where(x => x.CustomerId == customerId && notificationIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        foreach (var notification in notifications)
        {
            notification.MarkAsRead();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return counts;
    }

    public async Task<IReadOnlyList<Document>> GetGeneralDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var termsDocIds = await _dbContext.Terms
            .AsNoTracking()
            .Select(t => t.DocumentId)
            .ToListAsync(cancellationToken);

        var docs = await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.Status != DocumentStatus.Deleted &&
                (d.SourceType == "Legal" ||
                 d.SourceType == "General" ||
                 d.SourceType == "Bank" ||
                 d.DocumentType == "Agreement" ||
                 d.DocumentType == "GeneralTerms" ||
                 d.DocumentType == "PrivacyPolicy" ||
                 termsDocIds.Contains(d.Id)))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        return docs;
    }

    public async Task<int> AutoArchiveOldReadThreadsAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);

        var oldReadStates = await (from s in _dbContext.MessageThreadStates
                                   join t in _dbContext.MessageThreads on s.ThreadId equals t.Id
                                   where s.CustomerId == customerId &&
                                         s.Folder == MessageFolder.Inbox &&
                                         s.ReadAt != null &&
                                         t.CreatedAt < cutoff
                                   select s)
            .ToListAsync(cancellationToken);

        if (oldReadStates.Count == 0)
        {
            return 0;
        }

        foreach (var state in oldReadStates)
        {
            state.Archive();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return oldReadStates.Count;
    }

    private async Task<FeedItem> UpsertFeedItemAsync(
        long customerId,
        FeedItemType itemType,
        long sourceId,
        string title,
        string? preview,
        FeedPriority priority,
        bool actionRequired,
        DateTimeOffset occurredAt,
        CancellationToken cancellationToken)
    {
        var item = await _dbContext.FeedItems.FirstOrDefaultAsync(
            x => x.CustomerId == customerId && x.ItemType == itemType && x.SourceId == sourceId,
            cancellationToken);

        if (item is null)
        {
            item = new FeedItem(
                customerId,
                itemType,
                sourceId,
                title,
                preview,
                priority,
                actionRequired,
                occurredAt);
            _dbContext.FeedItems.Add(item);
        }
        else
        {
            item.Update(title, preview, priority, actionRequired, occurredAt);
        }

        return item;
    }

    private static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];

    private static FeedItemType? ParseFeedItemTypeFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "thread" or "threads" or "message" => FeedItemType.Message,
            "notification" or "notifications" => FeedItemType.Notification,
            "document" or "documents" => FeedItemType.Document,
            "term" or "terms" => FeedItemType.Terms,
            _ => throw new ValidationException($"Okänd inbox-typ: '{value}'.")
        };
    }

    private static string MapFeedItemType(FeedItemType itemType) => itemType switch
    {
        FeedItemType.Message => "thread",
        FeedItemType.Notification => "notification",
        FeedItemType.Document => "document",
        FeedItemType.Terms => "term",
        _ => throw new ArgumentOutOfRangeException(nameof(itemType), itemType, "Unsupported feed item type.")
    };
}
