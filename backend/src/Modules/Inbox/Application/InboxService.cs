using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Agreements.Domain;
using Nordiska.Modules.Communication.Domain;
using Nordiska.Modules.Inbox.Contracts.Requests;
using Nordiska.Modules.Inbox.Contracts.Responses;

namespace Nordiska.Modules.Inbox.Application;

public sealed class InboxService(
    IInboxRepository repository,
    IValidator<CreateThreadRequest> createThreadValidator,
    IValidator<ReplyThreadRequest> replyThreadValidator,
    IValidator<StaffReplyRequest> staffReplyValidator,
    IValidator<CreateAdminThreadRequest> createAdminThreadValidator) : IInboxService
{
    private readonly IInboxRepository _repository = repository;
    private readonly IValidator<CreateThreadRequest> _createThreadValidator = createThreadValidator;
    private readonly IValidator<ReplyThreadRequest> _replyThreadValidator = replyThreadValidator;
    private readonly IValidator<StaffReplyRequest> _staffReplyValidator = staffReplyValidator;
    private readonly IValidator<CreateAdminThreadRequest> _createAdminThreadValidator = createAdminThreadValidator;

    public async Task<PagedResult<ThreadSummaryResponse>> GetThreadsAsync(
        long customerId,
        InboxQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        await _repository.AutoArchiveOldReadThreadsAsync(customerId, cancellationToken);

        var folder = ParseFolder(parameters.Folder);
        var page = parameters.Page < 1 ? 1 : parameters.Page;
        var pageSize = parameters.PageSize is < 1 or > 100 ? 20 : parameters.PageSize;

        var pagedThreads = await _repository.GetThreadsByCustomerIdAsync(
            customerId,
            folder,
            parameters.SearchTerm,
            page,
            pageSize,
            cancellationToken);

        var summaries = new List<ThreadSummaryResponse>(pagedThreads.Items.Count);

        foreach (var thread in pagedThreads.Items)
        {
            var state = await _repository.GetThreadStateAsync(thread.Id, customerId, cancellationToken);
            var messages = await _repository.GetMessagesByThreadIdAsync(thread.Id, cancellationToken);
            var lastMessage = messages.LastOrDefault();
            var canReply = thread.Status == MessageThreadStatus.Open && (lastMessage is null || lastMessage.ReplyAllowed);

            summaries.Add(new ThreadSummaryResponse(
                Id: thread.Id,
                Subject: thread.Subject,
                Status: thread.Status.ToString(),
                CreatedAt: thread.CreatedAt,
                LastMessageAt: thread.LastMessageAt,
                IsRead: state?.IsRead ?? false,
                Folder: (state?.Folder ?? MessageFolder.Inbox).ToString(),
                MessageCount: messages.Count,
                IsInformationOnly: thread.IsInformationOnly,
                CanReply: canReply,
                Category: thread.Category));
        }

        return PagedResult<ThreadSummaryResponse>.Create(
            summaries,
            pagedThreads.TotalCount,
            pagedThreads.Page,
            pagedThreads.PageSize);
    }

    public async Task<ThreadDetailResponse?> GetThreadDetailsAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken = default)
    {
        var thread = await _repository.GetThreadByIdAsync(threadId, cancellationToken);
        if (thread is null)
        {
            return null;
        }

        var state = await _repository.GetThreadStateAsync(threadId, customerId, cancellationToken);
        if (state is null)
        {
            // Customer is not part of this thread -> return null for 404
            return null;
        }

        if (!state.IsRead)
        {
            await _repository.MarkAsReadAsync(threadId, customerId, cancellationToken);
            state.MarkAsRead();
        }

        var messages = await _repository.GetMessagesByThreadIdAsync(threadId, cancellationToken);
        var lastMessage = messages.LastOrDefault();
        var canReply = thread.Status == MessageThreadStatus.Open && (lastMessage is null || lastMessage.ReplyAllowed);

        var messageResponses = messages.Select(MapToResponse).ToList();

        return new ThreadDetailResponse(
            Id: thread.Id,
            Subject: thread.Subject,
            Status: thread.Status.ToString(),
            CreatedAt: thread.CreatedAt,
            LastMessageAt: thread.LastMessageAt,
            IsRead: state.IsRead,
            Folder: state.Folder.ToString(),
            CanReply: canReply,
            IsInformationOnly: thread.IsInformationOnly,
            Category: thread.Category,
            Messages: messageResponses);
    }

    public async Task<ThreadDetailResponse> CreateSupportTicketAsync(
        long customerId,
        CreateThreadRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createThreadValidator.ValidateAndThrowAsync(request, cancellationToken);

        var sanitizedSubject = SensitiveDataSanitizer.Sanitize(request.Subject);
        var sanitizedBody = SensitiveDataSanitizer.Sanitize(request.Body);

        var subject = string.IsNullOrWhiteSpace(request.Category)
            ? sanitizedSubject
            : sanitizedSubject.StartsWith($"[{request.Category}]", StringComparison.OrdinalIgnoreCase)
                ? sanitizedSubject
                : $"[{request.Category}] {sanitizedSubject}";

        var thread = await _repository.CreateThreadWithInitialMessageAsync(
            customerId,
            subject,
            sanitizedBody,
            replyAllowed: true,
            isInformationOnly: false,
            category: request.Category,
            cancellationToken: cancellationToken);

        var messages = await _repository.GetMessagesByThreadIdAsync(thread.Id, cancellationToken);
        var messageResponses = messages.Select(MapToResponse).ToList();

        return new ThreadDetailResponse(
            Id: thread.Id,
            Subject: thread.Subject,
            Status: thread.Status.ToString(),
            CreatedAt: thread.CreatedAt,
            LastMessageAt: thread.LastMessageAt,
            IsRead: true,
            Folder: MessageFolder.Inbox.ToString(),
            CanReply: true,
            IsInformationOnly: false,
            Category: request.Category,
            Messages: messageResponses);
    }

    public async Task<MessageResponse?> ReplyToThreadAsync(
        long customerId,
        long threadId,
        ReplyThreadRequest request,
        CancellationToken cancellationToken = default)
    {
        await _replyThreadValidator.ValidateAndThrowAsync(request, cancellationToken);

        var thread = await _repository.GetThreadByIdAsync(threadId, cancellationToken);
        if (thread is null)
        {
            return null;
        }

        var state = await _repository.GetThreadStateAsync(threadId, customerId, cancellationToken);
        if (state is null)
        {
            return null;
        }

        if (thread.Status == MessageThreadStatus.Closed)
        {
            throw new InvalidOperationException("Det går inte att svara på ett stängt ärende.");
        }

        var messages = await _repository.GetMessagesByThreadIdAsync(threadId, cancellationToken);
        var lastMessage = messages.LastOrDefault();
        if (lastMessage is not null && !lastMessage.ReplyAllowed)
        {
            throw new InvalidOperationException("Svar är inte tillåtet på detta meddelande.");
        }

        var sanitizedBody = SensitiveDataSanitizer.Sanitize(request.Body);

        var message = await _repository.AddMessageAsync(
            threadId,
            MessageSenderType.Customer,
            senderCustomerId: customerId,
            body: sanitizedBody,
            replyAllowed: true,
            cancellationToken);

        await _repository.MarkAsReadAsync(threadId, customerId, cancellationToken);

        return MapToResponse(message);
    }

    public async Task<bool> MarkAsReadAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken = default)
    {
        var state = await _repository.GetThreadStateAsync(threadId, customerId, cancellationToken);
        if (state is null)
        {
            return false;
        }

        await _repository.MarkAsReadAsync(threadId, customerId, cancellationToken);
        return true;
    }

    public async Task<UnreadCountResponse> GetUnreadCountAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var counts = await _repository.GetSummaryCountsAsync(customerId, cancellationToken);
        if (counts is null)
        {
            var fallbackCount = await _repository.GetUnreadCountAsync(customerId, cancellationToken);
            return new UnreadCountResponse(fallbackCount, fallbackCount, fallbackCount, 0, 0, 0);
        }

        return new UnreadCountResponse(
            UnreadCount: counts.TotalUnread,
            TotalUnread: counts.TotalUnread,
            UnreadThreads: counts.UnreadThreads,
            UnreadNotifications: counts.UnreadNotifications,
            UnopenedDocuments: counts.UnopenedDocuments,
            PendingTerms: counts.PendingTerms,
            UnreadDocuments: counts.UnreadDocuments,
            UnreadTerms: counts.UnreadTerms,
            ActionRequired: counts.ActionRequired);
    }

    public async Task<bool> ArchiveThreadAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken = default)
    {
        var state = await _repository.GetThreadStateAsync(threadId, customerId, cancellationToken);
        if (state is null)
        {
            return false;
        }

        await _repository.ArchiveThreadAsync(threadId, customerId, cancellationToken);
        return true;
    }

    public async Task<bool> RestoreThreadAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken = default)
    {
        var state = await _repository.GetThreadStateAsync(threadId, customerId, cancellationToken);
        if (state is null)
        {
            return false;
        }

        await _repository.MoveToInboxAsync(threadId, customerId, cancellationToken);
        return true;
    }

    public async Task<bool> CloseThreadAsync(
        long customerId,
        long threadId,
        bool isStaff,
        CancellationToken cancellationToken = default)
    {
        if (!isStaff)
        {
            var state = await _repository.GetThreadStateAsync(threadId, customerId, cancellationToken);
            if (state is null)
            {
                return false;
            }
        }

        return await _repository.CloseThreadAsync(threadId, cancellationToken);
    }

    public async Task<bool> ReopenThreadAsync(
        long customerId,
        long threadId,
        bool isStaff,
        CancellationToken cancellationToken = default)
    {
        if (!isStaff)
        {
            var state = await _repository.GetThreadStateAsync(threadId, customerId, cancellationToken);
            if (state is null)
            {
                return false;
            }
        }

        return await _repository.ReopenThreadAsync(threadId, cancellationToken);
    }

    public async Task<PagedResult<AdminThreadSummaryResponse>> GetAdminThreadsAsync(
        AdminThreadQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetAdminThreadsAsync(parameters, cancellationToken);
        var mapped = result.Items.Select(item => new AdminThreadSummaryResponse(
            Id: item.Thread.Id,
            CustomerId: item.CustomerId,
            CustomerName: $"Kund {item.CustomerId}",
            Subject: item.Thread.Subject,
            Status: item.Thread.Status.ToString(),
            Category: item.Thread.Category,
            IsInformationOnly: item.Thread.IsInformationOnly,
            CanReply: item.Thread.Status == MessageThreadStatus.Open,
            MessageCount: item.MessageCount,
            CreatedAt: item.Thread.CreatedAt,
            LastMessageAt: item.Thread.LastMessageAt
        )).ToList();

        return PagedResult<AdminThreadSummaryResponse>.Create(
            mapped,
            result.TotalCount,
            result.Page,
            result.PageSize);
    }

    public async Task<ThreadDetailResponse?> GetAdminThreadDetailsAsync(
        long threadId,
        CancellationToken cancellationToken = default)
    {
        var thread = await _repository.GetThreadByIdAsync(threadId, cancellationToken);
        if (thread is null)
        {
            return null;
        }

        var messages = await _repository.GetMessagesByThreadIdAsync(threadId, cancellationToken);
        var lastMessage = messages.LastOrDefault();
        var canReply = thread.Status == MessageThreadStatus.Open && (lastMessage is null || lastMessage.ReplyAllowed);

        var messageResponses = messages.Select(MapToResponse).ToList();

        return new ThreadDetailResponse(
            Id: thread.Id,
            Subject: thread.Subject,
            Status: thread.Status.ToString(),
            CreatedAt: thread.CreatedAt,
            LastMessageAt: thread.LastMessageAt,
            IsRead: true,
            Folder: MessageFolder.Inbox.ToString(),
            CanReply: canReply,
            IsInformationOnly: thread.IsInformationOnly,
            Category: thread.Category,
            Messages: messageResponses);
    }

    public async Task<int> CreateAdminThreadAsync(
        CreateAdminThreadRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createAdminThreadValidator.ValidateAndThrowAsync(request, cancellationToken);

        var targetCustomerIds = new List<long>();
        if (request.BroadcastToAll)
        {
            var allIds = await _repository.GetAllCustomerIdsAsync(cancellationToken);
            targetCustomerIds.AddRange(allIds);
        }
        else if (request.CustomerId.HasValue)
        {
            targetCustomerIds.Add(request.CustomerId.Value);
        }

        if (targetCustomerIds.Count == 0 && request.CustomerId.HasValue)
        {
            targetCustomerIds.Add(request.CustomerId.Value);
        }

        var sanitizedSubject = SensitiveDataSanitizer.Sanitize(request.Subject);
        var sanitizedBody = SensitiveDataSanitizer.Sanitize(request.Body);

        var createdCount = 0;
        foreach (var custId in targetCustomerIds)
        {
            var thread = await _repository.CreateThreadWithInitialMessageAsync(
                customerId: custId,
                subject: sanitizedSubject,
                initialMessageBody: sanitizedBody,
                replyAllowed: request.ReplyAllowed,
                isInformationOnly: request.IsInformationOnly,
                category: request.Category,
                senderType: MessageSenderType.Bank,
                cancellationToken: cancellationToken);

            // Mark as unread for the customer
            await _repository.MarkAsUnreadAsync(thread.Id, custId, cancellationToken);

            // Add customer notification
            await _repository.AddNotificationAsync(
                customerId: custId,
                type: request.IsInformationOnly ? "announcement" : "bank_message",
                title: request.Subject,
                body: request.Body.Length > 200 ? request.Body[..200] + "..." : request.Body,
                priority: request.IsInformationOnly ? NotificationPriority.High : NotificationPriority.Normal,
                targetType: NotificationTargetType.MessageThread,
                targetId: thread.Id,
                cancellationToken: cancellationToken);

            createdCount++;
        }

        return createdCount;
    }

    public async Task<PagedResult<CustomerNotificationResponse>> GetNotificationsAsync(
        long customerId,
        NotificationQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetCustomerNotificationsAsync(
            customerId,
            parameters.UnreadOnly,
            parameters.Page,
            parameters.PageSize,
            cancellationToken);

        var mapped = result.Items.Select(n => new CustomerNotificationResponse(
            Id: n.Id,
            CustomerId: n.CustomerId,
            Type: n.Type,
            Priority: n.Priority.ToString(),
            Title: n.Title,
            Body: n.Body,
            TargetType: n.TargetType?.ToString(),
            TargetId: n.TargetId,
            IsRead: n.IsRead,
            CreatedAt: n.CreatedAt,
            ReadAt: n.ReadAt
        )).ToList();

        return PagedResult<CustomerNotificationResponse>.Create(
            mapped,
            result.TotalCount,
            result.Page,
            result.PageSize);
    }

    public async Task CreateSavingsGoalCompletedNotificationAsync(
        long customerId,
        long savingsGoalId,
        string goalTitle,
        CancellationToken cancellationToken = default)
    {
        await _repository.AddNotificationAsync(
            customerId,
            "savings_goal_completed",
            "Du har nått ditt sparmål!",
            $"Grattis! Sparmålet \"{goalTitle}\" är nu uppnått.",
            NotificationPriority.Normal,
            NotificationTargetType.SavingsGoal,
            savingsGoalId,
            cancellationToken);
    }

    public async Task<bool> MarkNotificationReadAsync(
        long customerId,
        long notificationId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.MarkNotificationReadAsync(customerId, notificationId, cancellationToken);
    }

    public async Task<int> MarkAllNotificationsReadAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        return await _repository.MarkAllNotificationsReadAsync(customerId, cancellationToken);
    }

    public async Task<MessageResponse?> AddStaffReplyAsync(
        long staffId,
        long threadId,
        StaffReplyRequest request,
        CancellationToken cancellationToken = default)
    {
        await _staffReplyValidator.ValidateAndThrowAsync(request, cancellationToken);

        var thread = await _repository.GetThreadByIdAsync(threadId, cancellationToken);
        if (thread is null)
        {
            return null;
        }

        var sanitizedBody = SensitiveDataSanitizer.Sanitize(request.Body);

        var message = await _repository.AddMessageAsync(
            threadId,
            MessageSenderType.Bank,
            senderCustomerId: staffId,
            body: sanitizedBody,
            replyAllowed: request.ReplyAllowed,
            cancellationToken);

        var states = await _repository.GetAllThreadStatesAsync(threadId, cancellationToken);
        foreach (var state in states)
        {
            // Reset read status and ensure moved to inbox
            await _repository.MoveToInboxAsync(threadId, state.CustomerId, cancellationToken);
            await _repository.MarkAsUnreadAsync(threadId, state.CustomerId, cancellationToken);

            await _repository.AddNotificationAsync(
                customerId: state.CustomerId,
                type: "support_message",
                title: "Nytt svar i ditt ärende",
                body: $"Handläggare har svarat i ärendet: {thread.Subject}",
                priority: NotificationPriority.Normal,
                targetType: NotificationTargetType.MessageThread,
                targetId: threadId,
                cancellationToken: cancellationToken);
        }

        return MapToResponse(message);
    }

    private static MessageResponse MapToResponse(Message message)
    {
        var senderName = message.SenderType switch
        {
            MessageSenderType.System => "Nordiska Sparbanken",
            MessageSenderType.Bank => "Nordiska Sparbanken",
            MessageSenderType.Customer => "Kund",
            _ => message.SenderType.ToString()
        };

        return new MessageResponse(
            Id: message.Id,
            ThreadId: message.ThreadId,
            SenderType: message.SenderType.ToString(),
            SenderName: senderName,
            SenderCustomerId: message.SenderCustomerId,
            Body: message.Body,
            ReplyAllowed: message.ReplyAllowed,
            SentAt: message.SentAt);
    }

    public async Task<PagedResult<DocumentResponse>> GetDocumentsAsync(
        long customerId,
        DocumentQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await _repository.GetCustomerDocumentsAsync(
            customerId,
            parameters.Year,
            parameters.DocumentType,
            parameters.Page,
            parameters.PageSize,
            cancellationToken);

        var mapped = result.Items.Select(item => new DocumentResponse(
            Id: item.CustomerDoc.Id,
            DocumentId: item.Doc.Id,
            DocumentType: item.Doc.DocumentType,
            Title: item.Doc.Title,
            FileName: item.Doc.FileName,
            MimeType: item.Doc.MimeType,
            FileSizeBytes: item.Doc.FileSizeBytes,
            Sha256: item.Doc.Sha256,
            Status: item.Doc.Status.ToString(),
            PublishedAt: item.CustomerDoc.PublishedAt,
            FirstOpenedAt: item.CustomerDoc.FirstOpenedAt,
            HasBeenOpened: item.CustomerDoc.HasBeenOpened
        )).ToList();

        return PagedResult<DocumentResponse>.Create(
            mapped,
            result.TotalCount,
            result.Page,
            result.PageSize);
    }

    public async Task<DocumentDownloadResult?> DownloadDocumentAsync(
        long customerId,
        long documentId,
        CancellationToken cancellationToken = default)
    {
        var (customerDoc, doc) = await _repository.GetCustomerDocumentByIdAsync(customerId, documentId, cancellationToken);
        if (customerDoc is null || doc is null)
        {
            return null;
        }

        byte[] content;
        if (!string.IsNullOrWhiteSpace(doc.StorageKey) && File.Exists(doc.StorageKey))
        {
            content = await File.ReadAllBytesAsync(doc.StorageKey, cancellationToken);
        }
        else
        {
            // Generate standard compliant PDF content containing document details
            content = GenerateFallbackPdf(doc);
        }

        var computedHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var expectedHash = doc.Sha256?.Trim().ToLowerInvariant();
        var isValid = string.IsNullOrWhiteSpace(expectedHash) || string.Equals(computedHash, expectedHash, StringComparison.OrdinalIgnoreCase);

        // Mark document as opened for legal and audit traceability (NOR-252)
        await _repository.MarkDocumentOpenedAsync(customerDoc.Id, cancellationToken);

        return new DocumentDownloadResult(
            FileName: doc.FileName,
            MimeType: doc.MimeType,
            Content: content,
            Sha256: computedHash,
            IsChecksumValid: isValid);
    }

    private static byte[] GenerateFallbackPdf(Nordiska.Modules.Documents.Domain.Document doc)
    {
        var pdfText = $"%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n" +
                      $"3 0 obj<</Type/Page/MediaBox[0 0 595 842]/Parent 2 0 R/Resources<<>>>>endobj\n" +
                      $"xref\n0 4\n0000000000 65535 f\n0000000010 00000 n\n0000000053 00000 n\n0000000102 00000 n\n" +
                      $"trailer<</Size 4/Root 1 0 R>>\nstartxref\n178\n%%EOF\n% Document: {doc.Title}\n";
        return Encoding.UTF8.GetBytes(pdfText);
    }

    public async Task<IReadOnlyList<PendingTermResponse>> GetPendingTermsAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var pending = await _repository.GetPendingTermsAsync(customerId, cancellationToken);
        return pending.Select(item => new PendingTermResponse(
            AcceptanceId: item.Acceptance.Id,
            TermId: item.Term.Id,
            Code: item.Term.Code,
            Version: item.Term.Version,
            Title: item.Term.Title,
            DocumentId: item.Term.DocumentId,
            EffectiveFrom: item.Term.EffectiveFrom,
            PublishedAt: item.Term.PublishedAt,
            Status: item.Acceptance.Status.ToString(),
            CreatedAt: item.Acceptance.CreatedAt,
            DownloadUrl: $"/api/inbox/documents/{item.Term.DocumentId}/download"
        )).ToList();
    }

    public async Task<TermAcceptanceResult> AcceptTermAsync(
        long customerId,
        long termId,
        CancellationToken cancellationToken = default)
    {
        var (acceptance, term) = await _repository.GetTermAcceptanceAsync(customerId, termId, cancellationToken);
        if (acceptance is null || term is null)
        {
            return new TermAcceptanceResult(
                AcceptanceId: 0,
                TermId: termId,
                CustomerId: customerId,
                Status: "NotFound",
                AcceptedAt: null,
                Success: false,
                Message: $"Villkor eller samtycke med id '{termId}' kunde inte hittas.");
        }

        if (acceptance.Status == TermAcceptanceStatus.Accepted)
        {
            return new TermAcceptanceResult(
                AcceptanceId: acceptance.Id,
                TermId: term.Id,
                CustomerId: customerId,
                Status: acceptance.Status.ToString(),
                AcceptedAt: acceptance.AcceptedAt,
                Success: true,
                Message: "Villkoren har redan godkänts tidigare.");
        }

        var updated = await _repository.AcceptTermAsync(acceptance, cancellationToken);

        // Send confirmation notification to customer inbox
        await _repository.AddNotificationAsync(
            customerId,
            "terms_accepted",
            $"Villkor godkända: {term.Title}",
            $"Du godkände {term.Title} (version {term.Version}) den {updated.AcceptedAt:yyyy-MM-dd HH:mm}.",
            NotificationPriority.Normal,
            NotificationTargetType.Document,
            term.DocumentId,
            cancellationToken);

        return new TermAcceptanceResult(
            AcceptanceId: updated.Id,
            TermId: term.Id,
            CustomerId: customerId,
            Status: updated.Status.ToString(),
            AcceptedAt: updated.AcceptedAt,
            Success: true,
            Message: $"Villkor '{term.Title}' har godkänts.");
    }

    public async Task<TermResponse> PublishTermAsync(
        PublishTermRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var effectiveFrom = request.EffectiveFrom ?? DateTimeOffset.UtcNow;
        var term = await _repository.PublishTermAsync(
            request.Code,
            request.Version,
            request.Title,
            request.DocumentId,
            effectiveFrom,
            null,
            cancellationToken);

        return new TermResponse(
            Id: term.Id,
            Code: term.Code,
            Version: term.Version,
            Title: term.Title,
            DocumentId: term.DocumentId,
            Status: term.Status.ToString(),
            EffectiveFrom: term.EffectiveFrom,
            PublishedAt: term.PublishedAt,
            DownloadUrl: $"/api/inbox/documents/{term.DocumentId}/download");
    }

    public async Task<InboxOverviewResponse> GetOverviewAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        await _repository.AutoArchiveOldReadThreadsAsync(customerId, cancellationToken);

        var counts = await _repository.GetSummaryCountsAsync(customerId, cancellationToken);
        var feedResult = await _repository.GetUnifiedFeedAsync(
            customerId,
            new FeedQueryParameters(Page: 1, PageSize: 20),
            cancellationToken);
        var unreadFeedResult = await _repository.GetUnifiedFeedAsync(
            customerId,
            new FeedQueryParameters(UnreadOnly: true, Page: 1, PageSize: 20),
            cancellationToken);
        var pendingTerms = await GetPendingTermsAsync(customerId, cancellationToken);

        return new InboxOverviewResponse(
            Counts: counts,
            Feed: feedResult.Items.ToList(),
            UnreadFeed: unreadFeedResult.Items.ToList(),
            PendingTerms: pendingTerms);
    }

    public async Task<IReadOnlyList<GeneralDocumentResponse>> GetGeneralDocumentsAsync(
        CancellationToken cancellationToken = default)
    {
        var docs = await _repository.GetGeneralDocumentsAsync(cancellationToken);
        return docs.Select(d => new GeneralDocumentResponse(
            Id: d.Id,
            DocumentType: d.DocumentType,
            Title: d.Title,
            FileName: d.FileName,
            MimeType: d.MimeType,
            FileSizeBytes: d.FileSizeBytes,
            Sha256: d.Sha256,
            PublishedAt: d.CreatedAt,
            DownloadUrl: $"/api/inbox/documents/{d.Id}/download"
        )).ToList();
    }

    public async Task<PagedResult<InboxFeedItemResponse>> GetFeedAsync(
        long customerId,
        FeedQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetUnifiedFeedAsync(customerId, parameters, cancellationToken);
    }

    public async Task<bool> MarkFeedItemAsReadAsync(
        long customerId,
        string feedId,
        CancellationToken cancellationToken = default)
    {
        if (!FeedItemIdentity.TryParse(feedId, out var itemType, out var sourceId))
        {
            return false;
        }

        return await _repository.MarkFeedItemReadAsync(
            customerId,
            itemType,
            sourceId,
            cancellationToken);
    }

    public async Task<MarkAllReadResponse> MarkAllAsReadAsync(
        long customerId,
        CancellationToken cancellationToken = default)
    {
        var counts = await _repository.MarkAllFeedItemsReadAsync(customerId, cancellationToken);

        return new MarkAllReadResponse(
            Success: true,
            ThreadsMarkedAsRead: counts.Threads,
            NotificationsMarkedAsRead: counts.Notifications,
            TotalMarkedAsRead: counts.Total,
            Message: counts.Total > 0
                ? $"{counts.Total} objekt markerades som lästa."
                : "Alla inboxobjekt är redan lästa.",
            DocumentsMarkedAsRead: counts.Documents,
            TermsMarkedAsRead: counts.Terms);
    }

    private static MessageFolder ParseFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return MessageFolder.Inbox;
        }

        return folder.ToLowerInvariant() switch
        {
            "archive" or "arkiv" => MessageFolder.Archive,
            "sent" or "skickat" => MessageFolder.Sent,
            _ => MessageFolder.Inbox
        };
    }
}
