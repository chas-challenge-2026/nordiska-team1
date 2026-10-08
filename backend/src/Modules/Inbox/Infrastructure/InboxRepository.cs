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
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

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
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return term;
    }

    public async Task<InboxSummaryCounts> GetSummaryCountsAsync(long customerId, CancellationToken cancellationToken = default)
    {
        var unreadThreads = await _dbContext.MessageThreadStates
            .AsNoTracking()
            .CountAsync(s => s.CustomerId == customerId && s.Folder == MessageFolder.Inbox && s.ReadAt == null, cancellationToken);

        var unreadNotifications = await _dbContext.CustomerNotifications
            .AsNoTracking()
            .CountAsync(n => n.CustomerId == customerId && n.ReadAt == null, cancellationToken);

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

        var totalUnread = unreadThreads + unreadNotifications + unopenedDocuments + pendingTerms;

        return new InboxSummaryCounts(
            TotalUnread: totalUnread,
            UnreadThreads: unreadThreads,
            UnreadNotifications: unreadNotifications,
            UnopenedDocuments: unopenedDocuments,
            PendingTerms: pendingTerms);
    }

    public async Task<PagedResult<InboxFeedItemResponse>> GetUnifiedFeedAsync(
        long customerId,
        FeedQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var items = new List<InboxFeedItemResponse>();
        var filterType = parameters.Type?.Trim().ToLowerInvariant();

        // 1. Threads
        if (string.IsNullOrEmpty(filterType) || filterType is "thread" or "threads" or "message")
        {
            var states = await _dbContext.MessageThreadStates
                .AsNoTracking()
                .Where(s => s.CustomerId == customerId)
                .ToListAsync(cancellationToken);

            var threadIds = states.Select(s => s.ThreadId).ToList();

            var threadsList = await _dbContext.MessageThreads
                .AsNoTracking()
                .Where(t => threadIds.Contains(t.Id))
                .OrderByDescending(t => t.LastMessageAt)
                .Take(100)
                .ToListAsync(cancellationToken);

            var stateByThreadId = states.ToDictionary(s => s.ThreadId);

            foreach (var thread in threadsList)
            {
                var state = stateByThreadId.GetValueOrDefault(thread.Id);
                var isRead = state?.IsRead ?? false;

                var lastMsg = await _dbContext.Messages
                    .AsNoTracking()
                    .Where(m => m.ThreadId == thread.Id && m.RevokedAt == null)
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => m.Body)
                    .FirstOrDefaultAsync(cancellationToken);

                var preview = lastMsg is not null && lastMsg.Length > 120
                    ? string.Concat(lastMsg.AsSpan(0, 120), "...")
                    : lastMsg;

                items.Add(new InboxFeedItemResponse(
                    Id: $"thread-{thread.Id}",
                    Type: "thread",
                    SourceId: thread.Id,
                    Title: thread.Subject,
                    Preview: preview,
                    Category: thread.Category ?? "Meddelande",
                    Priority: "Normal",
                    IsRead: isRead,
                    ActionRequired: false,
                    OccurredAt: thread.LastMessageAt,
                    TargetUrl: $"/api/inbox/threads/{thread.Id}"));
            }
        }

        // 2. Notifications
        if (string.IsNullOrEmpty(filterType) || filterType is "notification" or "notifications")
        {
            var notifs = await _dbContext.CustomerNotifications
                .AsNoTracking()
                .Where(n => n.CustomerId == customerId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(100)
                .ToListAsync(cancellationToken);

            foreach (var n in notifs)
            {
                var priorityStr = n.Priority switch
                {
                    NotificationPriority.Critical => "Critical",
                    NotificationPriority.High => "Important",
                    _ => "Normal"
                };

                items.Add(new InboxFeedItemResponse(
                    Id: $"notification-{n.Id}",
                    Type: "notification",
                    SourceId: n.Id,
                    Title: n.Title,
                    Preview: n.Body,
                    Category: n.Type,
                    Priority: priorityStr,
                    IsRead: n.IsRead,
                    ActionRequired: n.Priority == NotificationPriority.Critical,
                    OccurredAt: n.CreatedAt,
                    TargetUrl: n.TargetType == NotificationTargetType.MessageThread && n.TargetId.HasValue
                        ? $"/api/inbox/threads/{n.TargetId}"
                        : null));
            }
        }

        // 3. Documents
        if (string.IsNullOrEmpty(filterType) || filterType is "document" or "documents")
        {
            var docs = await (from cd in _dbContext.CustomerDocuments
                              join d in _dbContext.Documents on cd.DocumentId equals d.Id
                              where cd.CustomerId == customerId && d.Status != DocumentStatus.Deleted
                              orderby cd.PublishedAt descending
                              select new { CustomerDoc = cd, Doc = d })
                .AsNoTracking()
                .Take(100)
                .ToListAsync(cancellationToken);

            foreach (var d in docs)
            {
                items.Add(new InboxFeedItemResponse(
                    Id: $"document-{d.Doc.Id}",
                    Type: "document",
                    SourceId: d.Doc.Id,
                    Title: d.Doc.Title,
                    Preview: $"{d.Doc.DocumentType} • {d.Doc.FileName}",
                    Category: d.Doc.DocumentType,
                    Priority: "Normal",
                    IsRead: d.CustomerDoc.HasBeenOpened,
                    ActionRequired: false,
                    OccurredAt: d.CustomerDoc.PublishedAt,
                    TargetUrl: $"/api/inbox/documents/{d.Doc.Id}/download"));
            }
        }

        // 4. Terms
        if (string.IsNullOrEmpty(filterType) || filterType is "term" or "terms")
        {
            var terms = await (from a in _dbContext.TermAcceptances
                               join t in _dbContext.Terms on a.TermId equals t.Id
                               where a.CustomerId == customerId && t.Status == TermStatus.Published
                               orderby t.PublishedAt descending
                               select new { Acceptance = a, Term = t })
                .AsNoTracking()
                .Take(50)
                .ToListAsync(cancellationToken);

            foreach (var t in terms)
            {
                var isAccepted = t.Acceptance.Status == TermAcceptanceStatus.Accepted;
                items.Add(new InboxFeedItemResponse(
                    Id: $"term-{t.Term.Id}",
                    Type: "term",
                    SourceId: t.Term.Id,
                    Title: t.Term.Title,
                    Preview: $"Version {t.Term.Version}. {(isAccepted ? "Godkänd." : "Inväntar digital acceptans.")}",
                    Category: "Villkor",
                    Priority: isAccepted ? "Normal" : "Important",
                    IsRead: isAccepted,
                    ActionRequired: !isAccepted,
                    OccurredAt: t.Term.PublishedAt,
                    TargetUrl: $"/api/inbox/terms/{t.Term.Id}/accept"));
            }
        }

        // Filter unread if requested
        IEnumerable<InboxFeedItemResponse> filtered = items;
        if (parameters.UnreadOnly)
        {
            filtered = filtered.Where(x => !x.IsRead || x.ActionRequired);
        }

        var sorted = filtered.OrderByDescending(x => x.OccurredAt).ToList();
        var totalCount = sorted.Count;
        var page = parameters.Page < 1 ? 1 : parameters.Page;
        var pageSize = parameters.PageSize is < 1 or > 100 ? 20 : parameters.PageSize;

        var pagedItems = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

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

        await _dbContext.SaveChangesAsync(cancellationToken);
        return unreadStates.Count;
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
}