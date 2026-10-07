using FluentValidation;
using Moq;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Communication.Domain;
using Nordiska.Modules.Inbox.Application;
using Nordiska.Modules.Inbox.Contracts.Requests;
using Nordiska.Modules.Inbox.Contracts.Validators;

namespace Nordiska.Modules.Inbox.Tests;

public class InboxServiceTests
{
    private readonly Mock<IInboxRepository> _repoMock;
    private readonly InboxService _service;

    public InboxServiceTests()
    {
        _repoMock = new Mock<IInboxRepository>();
        _service = new InboxService(
            _repoMock.Object,
            new CreateThreadRequestValidator(),
            new ReplyThreadRequestValidator(),
            new StaffReplyRequestValidator(),
            new CreateAdminThreadRequestValidator());
    }

    [Fact]
    public async Task CreateSupportTicket_WithCategory_PrefixesSubjectAndCreatesThread()
    {
        // Arrange
        const long customerId = 100;
        var request = new CreateThreadRequest("Räntefråga", "Hej, vad är min ränta?", "Sparkonto");

        var createdThread = new MessageThread(1, "[Sparkonto] Räntefråga");
        var initialMessage = new Message(createdThread.Id, MessageSenderType.Customer, request.Body, true, customerId);

        _repoMock.Setup(r => r.CreateThreadWithInitialMessageAsync(
                customerId,
                "[Sparkonto] Räntefråga",
                request.Body,
                true,
                false,
                "Sparkonto",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdThread);

        _repoMock.Setup(r => r.GetMessagesByThreadIdAsync(createdThread.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([initialMessage]);

        // Act
        var result = await _service.CreateSupportTicketAsync(customerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("[Sparkonto] Räntefråga", result.Subject);
        Assert.True(result.CanReply);
        Assert.Single(result.Messages);
        Assert.Equal(request.Body, result.Messages[0].Body);
    }

    [Fact]
    public async Task CreateSupportTicket_WithSparmalCategory_PrefixesSubjectAndCreatesThread()
    {
        // Arrange
        const long customerId = 100;
        var request = new CreateThreadRequest("Autosparande", "Hur pausar jag mitt sparmål?", "Sparmål");

        var createdThread = new MessageThread(2, "[Sparmål] Autosparande");
        var initialMessage = new Message(createdThread.Id, MessageSenderType.Customer, request.Body, true, customerId);

        _repoMock.Setup(r => r.CreateThreadWithInitialMessageAsync(
                customerId,
                "[Sparmål] Autosparande",
                request.Body,
                true,
                false,
                "Sparmål",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdThread);

        _repoMock.Setup(r => r.GetMessagesByThreadIdAsync(createdThread.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([initialMessage]);

        // Act
        var result = await _service.CreateSupportTicketAsync(customerId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("[Sparmål] Autosparande", result.Subject);
        Assert.True(result.CanReply);
        Assert.Single(result.Messages);
    }

    [Fact]
    public async Task CreateSupportTicket_WithInvalidCategory_ThrowsValidationException()
    {
        // Arrange
        const long customerId = 100;
        var request = new CreateThreadRequest("Ämne", "Meddelande här", "InvalidCategory");

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateSupportTicketAsync(customerId, request));
    }

    [Fact]
    public async Task ReplyToThread_WhenThreadClosed_ThrowsInvalidOperationException()
    {
        // Arrange
        const long customerId = 100;
        const long threadId = 5;
        var request = new ReplyThreadRequest("Ett svar");

        var thread = new MessageThread(1, "Ämne");
        thread.Close();

        var state = new MessageThreadState(threadId, customerId, MessageFolder.Inbox);

        _repoMock.Setup(r => r.GetThreadByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);
        _repoMock.Setup(r => r.GetThreadStateAsync(threadId, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ReplyToThreadAsync(customerId, threadId, request));

        Assert.Contains("stängt ärende", ex.Message);
    }

    [Fact]
    public async Task ReplyToThread_WhenReplyNotAllowed_ThrowsInvalidOperationException()
    {
        // Arrange
        const long customerId = 100;
        const long threadId = 5;
        var request = new ReplyThreadRequest("Ett svar");

        var thread = new MessageThread(1, "Ämne");
        var state = new MessageThreadState(threadId, customerId, MessageFolder.Inbox);
        var lastMessage = new Message(threadId, MessageSenderType.Bank, "Systemmeddelande", replyAllowed: false);

        _repoMock.Setup(r => r.GetThreadByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);
        _repoMock.Setup(r => r.GetThreadStateAsync(threadId, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        _repoMock.Setup(r => r.GetMessagesByThreadIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([lastMessage]);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ReplyToThreadAsync(customerId, threadId, request));

        Assert.Contains("Svar är inte tillåtet", ex.Message);
    }

    [Fact]
    public async Task ReplyToThread_WhenAllowed_AddsMessageAndMarksRead()
    {
        // Arrange
        const long customerId = 100;
        const long threadId = 5;
        var request = new ReplyThreadRequest("Ett svar");

        var thread = new MessageThread(1, "Ämne");
        var state = new MessageThreadState(threadId, customerId, MessageFolder.Inbox);
        var lastMessage = new Message(threadId, MessageSenderType.Bank, "Hur kan vi hjälpa dig?", replyAllowed: true);
        var replyMsg = new Message(threadId, MessageSenderType.Customer, request.Body, true, customerId);

        _repoMock.Setup(r => r.GetThreadByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);
        _repoMock.Setup(r => r.GetThreadStateAsync(threadId, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        _repoMock.Setup(r => r.GetMessagesByThreadIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([lastMessage]);
        _repoMock.Setup(r => r.AddMessageAsync(threadId, MessageSenderType.Customer, customerId, request.Body, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(replyMsg);

        // Act
        var result = await _service.ReplyToThreadAsync(customerId, threadId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Customer", result.SenderType);
        Assert.Equal(customerId, result.SenderCustomerId);
        _repoMock.Verify(r => r.MarkAsReadAsync(threadId, customerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetThreadDetails_WhenNotOwned_ReturnsNullFor404Isolation()
    {
        // Arrange
        const long customerId = 100;
        const long threadId = 5;

        var thread = new MessageThread(1, "Någon annans ärende");

        _repoMock.Setup(r => r.GetThreadByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);
        _repoMock.Setup(r => r.GetThreadStateAsync(threadId, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageThreadState?)null);

        // Act
        var result = await _service.GetThreadDetailsAsync(customerId, threadId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AddStaffReply_LogsStaffId_AndNotifiesCustomer()
    {
        // Arrange
        const long staffId = 999;
        const long customerId = 100;
        const long threadId = 5;
        var request = new StaffReplyRequest("Hej! Vi har undersökt ditt sparkonto.");

        var thread = new MessageThread(1, "Räntefråga");
        var customerState = new MessageThreadState(threadId, customerId, MessageFolder.Inbox);
        var replyMsg = new Message(threadId, MessageSenderType.Bank, request.Body, true, staffId);

        _repoMock.Setup(r => r.GetThreadByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);
        _repoMock.Setup(r => r.AddMessageAsync(threadId, MessageSenderType.Bank, staffId, request.Body, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(replyMsg);
        _repoMock.Setup(r => r.GetAllThreadStatesAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([customerState]);

        // Act
        var result = await _service.AddStaffReplyAsync(staffId, threadId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Bank", result.SenderType);
        Assert.Equal(staffId, result.SenderCustomerId);

        _repoMock.Verify(r => r.MoveToInboxAsync(threadId, customerId, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.MarkAsUnreadAsync(threadId, customerId, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AddNotificationAsync(
            customerId,
            "support_message",
            It.IsAny<string>(),
            It.IsAny<string>(),
            NotificationPriority.Normal,
            NotificationTargetType.MessageThread,
            threadId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetThreadDetails_WhenUnread_AutomaticallyMarksThreadAsRead()
    {
        // Arrange
        const long customerId = 100;
        const long threadId = 5;

        var thread = new MessageThread(threadId, "Räntebesked");
        var unreadState = new MessageThreadState(threadId, customerId, MessageFolder.Inbox);

        var systemMsg = new Message(threadId, MessageSenderType.System, "Räntan har uppdaterats.", false);

        _repoMock.Setup(r => r.GetThreadByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);
        _repoMock.Setup(r => r.GetThreadStateAsync(threadId, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unreadState);
        _repoMock.Setup(r => r.GetMessagesByThreadIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([systemMsg]);

        // Act
        var result = await _service.GetThreadDetailsAsync(customerId, threadId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsRead);
        _repoMock.Verify(r => r.MarkAsReadAsync(threadId, customerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetThreadDetails_MapsSenderName_CorrectlyForSystemBankAndCustomer()
    {
        // Arrange
        const long customerId = 100;
        const long threadId = 5;

        var thread = new MessageThread(threadId, "Kundärende");
        var readState = new MessageThreadState(threadId, customerId, MessageFolder.Inbox);
        readState.MarkAsRead();

        var customerMsg = new Message(threadId, MessageSenderType.Customer, "Fråga", true, customerId);
        var bankMsg = new Message(threadId, MessageSenderType.Bank, "Svar från banken", true, 999);
        var systemMsg = new Message(threadId, MessageSenderType.System, "Automatiskt kvitto", false);

        _repoMock.Setup(r => r.GetThreadByIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);
        _repoMock.Setup(r => r.GetThreadStateAsync(threadId, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readState);
        _repoMock.Setup(r => r.GetMessagesByThreadIdAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([customerMsg, bankMsg, systemMsg]);

        // Act
        var result = await _service.GetThreadDetailsAsync(customerId, threadId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Messages.Count);

        Assert.Equal("Customer", result.Messages[0].SenderType);
        Assert.Equal("Kund", result.Messages[0].SenderName);

        Assert.Equal("Bank", result.Messages[1].SenderType);
        Assert.Equal("Nordiska Sparbanken", result.Messages[1].SenderName);

        Assert.Equal("System", result.Messages[2].SenderType);
        Assert.Equal("Nordiska Sparbanken", result.Messages[2].SenderName);
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsCountFromRepository()
    {
        // Arrange
        const long customerId = 100;
        _repoMock.Setup(r => r.GetUnreadCountAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        // Act
        var result = await _service.GetUnreadCountAsync(customerId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.UnreadCount);
    }

    [Fact]
    public async Task RestoreThread_WhenThreadExists_MovesToInbox()
    {
        // Arrange
        const long customerId = 100;
        const long threadId = 5;
        var state = new MessageThreadState(threadId, customerId, MessageFolder.Archive);

        _repoMock.Setup(r => r.GetThreadStateAsync(threadId, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);

        // Act
        var success = await _service.RestoreThreadAsync(customerId, threadId);

        // Assert
        Assert.True(success);
        _repoMock.Verify(r => r.MoveToInboxAsync(threadId, customerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestoreThread_WhenNotOwned_ReturnsFalse()
    {
        // Arrange
        const long customerId = 100;
        const long threadId = 5;

        _repoMock.Setup(r => r.GetThreadStateAsync(threadId, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageThreadState?)null);

        // Act
        var success = await _service.RestoreThreadAsync(customerId, threadId);

        // Assert
        Assert.False(success);
        _repoMock.Verify(r => r.MoveToInboxAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetDocuments_ReturnsMappedDocuments()
    {
        // Arrange
        const long customerId = 100;
        var doc = new Nordiska.Modules.Documents.Domain.Document(
            "TaxReport",
            "Skatteunderlag 2025",
            "skatteunderlag_2025.pdf",
            "application/pdf",
            "docs/100/tax_2025.pdf",
            1024,
            "dummy-sha256",
            "System");
        var custDoc = new Nordiska.Modules.Documents.Domain.CustomerDocument(doc.Id, customerId);
        var pagedResult = PagedResult<(Nordiska.Modules.Documents.Domain.CustomerDocument CustomerDoc, Nordiska.Modules.Documents.Domain.Document Doc)>.Create(
            [(custDoc, doc)],
            1,
            1,
            20);

        _repoMock.Setup(r => r.GetCustomerDocumentsAsync(customerId, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var results = await _service.GetDocumentsAsync(customerId, new DocumentQueryParameters());

        // Assert
        Assert.NotNull(results);
        Assert.Single(results.Items);
        var item = results.Items.First();
        Assert.Equal("TaxReport", item.DocumentType);
        Assert.Equal("Skatteunderlag 2025", item.Title);
        Assert.Equal("skatteunderlag_2025.pdf", item.FileName);
        Assert.False(item.HasBeenOpened);
    }

    [Fact]
    public async Task DownloadDocument_WhenDocumentExists_ReturnsDownloadResultAndMarksOpened()
    {
        // Arrange
        const long customerId = 100;
        const long docId = 42;
        var doc = new Nordiska.Modules.Documents.Domain.Document(
            "AnnualStatement",
            "Årsbesked 2025",
            "arsbesked_2025.pdf",
            "application/pdf",
            "docs/100/arsbesked_2025.pdf",
            1024,
            "",
            "System");
        var custDoc = new Nordiska.Modules.Documents.Domain.CustomerDocument(docId, customerId);

        _repoMock.Setup(r => r.GetCustomerDocumentByIdAsync(customerId, docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((custDoc, doc));

        // Act
        var result = await _service.DownloadDocumentAsync(customerId, docId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("arsbesked_2025.pdf", result.FileName);
        Assert.Equal("application/pdf", result.MimeType);
        Assert.NotEmpty(result.Content);
        Assert.NotEmpty(result.Sha256);
        Assert.True(result.IsChecksumValid);
        _repoMock.Verify(r => r.MarkDocumentOpenedAsync(custDoc.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadDocument_WhenChecksumMismatches_ReturnsIsChecksumValidFalse()
    {
        // Arrange
        const long customerId = 100;
        const long docId = 43;
        var doc = new Nordiska.Modules.Documents.Domain.Document(
            "AnnualStatement",
            "Årsbesked 2025",
            "arsbesked_2025.pdf",
            "application/pdf",
            "docs/100/arsbesked_2025.pdf",
            1024,
            "tampered-hash",
            "System");
        var custDoc = new Nordiska.Modules.Documents.Domain.CustomerDocument(docId, customerId);

        _repoMock.Setup(r => r.GetCustomerDocumentByIdAsync(customerId, docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((custDoc, doc));

        // Act
        var result = await _service.DownloadDocumentAsync(customerId, docId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsChecksumValid);
    }

    [Fact]
    public async Task DownloadDocument_WhenDocumentNotFound_ReturnsNull()
    {
        // Arrange
        const long customerId = 100;
        const long docId = 999;

        _repoMock.Setup(r => r.GetCustomerDocumentByIdAsync(customerId, docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((Nordiska.Modules.Documents.Domain.CustomerDocument?)null, (Nordiska.Modules.Documents.Domain.Document?)null));

        // Act
        var result = await _service.DownloadDocumentAsync(customerId, docId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetPendingTerms_ReturnsMappedPendingTerms()
    {
        // Arrange
        const long customerId = 100;
        var term = new Nordiska.Modules.Agreements.Domain.Term(
            "TERMS_2026",
            2,
            "Allmänna kontovillkor 2026",
            50,
            DateTimeOffset.UtcNow);
        var acceptance = new Nordiska.Modules.Agreements.Domain.TermAcceptance(term.Id, customerId);

        _repoMock.Setup(r => r.GetPendingTermsAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([(acceptance, term)]);

        // Act
        var results = await _service.GetPendingTermsAsync(customerId);

        // Assert
        Assert.Single(results);
        Assert.Equal("TERMS_2026", results[0].Code);
        Assert.Equal("Allmänna kontovillkor 2026", results[0].Title);
        Assert.Equal("Pending", results[0].Status);
        Assert.Equal("/api/inbox/documents/50/download", results[0].DownloadUrl);
    }

    [Fact]
    public async Task AcceptTerm_WhenPending_AcceptsAndSendsNotification()
    {
        // Arrange
        const long customerId = 100;
        const long termId = 10;
        var term = new Nordiska.Modules.Agreements.Domain.Term(
            "TERMS_2026",
            2,
            "Allmänna kontovillkor 2026",
            50,
            DateTimeOffset.UtcNow);
        var acceptance = new Nordiska.Modules.Agreements.Domain.TermAcceptance(term.Id, customerId);

        _repoMock.Setup(r => r.GetTermAcceptanceAsync(customerId, termId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((acceptance, term));

        _repoMock.Setup(r => r.AcceptTermAsync(acceptance, It.IsAny<CancellationToken>()))
            .Callback<Nordiska.Modules.Agreements.Domain.TermAcceptance, CancellationToken>((ta, _) => ta.Accept())
            .ReturnsAsync(acceptance);

        // Act
        var result = await _service.AcceptTermAsync(customerId, termId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Accepted", result.Status);
        Assert.NotNull(result.AcceptedAt);
        _repoMock.Verify(r => r.AcceptTermAsync(acceptance, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AddNotificationAsync(
            customerId,
            "terms_accepted",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Nordiska.Modules.Communication.Domain.NotificationPriority>(),
            It.IsAny<Nordiska.Modules.Communication.Domain.NotificationTargetType?>(),
            It.IsAny<long?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptTerm_WhenAlreadyAccepted_ReturnsSuccess()
    {
        // Arrange
        const long customerId = 100;
        const long termId = 10;
        var term = new Nordiska.Modules.Agreements.Domain.Term(
            "TERMS_2026",
            2,
            "Allmänna kontovillkor 2026",
            50,
            DateTimeOffset.UtcNow);
        var acceptance = new Nordiska.Modules.Agreements.Domain.TermAcceptance(term.Id, customerId);
        acceptance.Accept();

        _repoMock.Setup(r => r.GetTermAcceptanceAsync(customerId, termId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((acceptance, term));

        // Act
        var result = await _service.AcceptTermAsync(customerId, termId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Accepted", result.Status);
        _repoMock.Verify(r => r.AcceptTermAsync(It.IsAny<Nordiska.Modules.Agreements.Domain.TermAcceptance>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AcceptTerm_WhenNotFound_ReturnsFailureResult()
    {
        // Arrange
        const long customerId = 100;
        const long termId = 999;

        _repoMock.Setup(r => r.GetTermAcceptanceAsync(customerId, termId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(((Nordiska.Modules.Agreements.Domain.TermAcceptance?)null, (Nordiska.Modules.Agreements.Domain.Term?)null));

        // Act
        var result = await _service.AcceptTermAsync(customerId, termId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("NotFound", result.Status);
    }

    [Fact]
    public async Task CloseThread_WhenCustomerOwnsThread_ClosesSuccessfully()
    {
        // Arrange
        const long customerId = 100;
        const long threadId = 5;
        var state = new MessageThreadState(threadId, customerId, MessageFolder.Inbox);

        _repoMock.Setup(r => r.GetThreadStateAsync(threadId, customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        _repoMock.Setup(r => r.CloseThreadAsync(threadId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.CloseThreadAsync(customerId, threadId, isStaff: false);

        // Assert
        Assert.True(result);
        _repoMock.Verify(r => r.CloseThreadAsync(threadId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAdminThread_SingleCustomer_CreatesThreadAndNotification()
    {
        // Arrange
        const long customerId = 100;
        var request = new CreateAdminThreadRequest(
            CustomerId: customerId,
            BroadcastToAll: false,
            Subject: "Räntejustering",
            Body: "Vi har justerat din sparränta.",
            Category: "Information",
            ReplyAllowed: false,
            IsInformationOnly: true);

        var createdThread = new MessageThread(1, "Räntejustering", isInformationOnly: true, category: "Information");

        _repoMock.Setup(r => r.CreateThreadWithInitialMessageAsync(
                customerId,
                request.Subject,
                request.Body,
                request.ReplyAllowed,
                request.IsInformationOnly,
                request.Category,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdThread);

        // Act
        var count = await _service.CreateAdminThreadAsync(request);

        // Assert
        Assert.Equal(1, count);
        _repoMock.Verify(r => r.MarkAsUnreadAsync(createdThread.Id, customerId, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AddNotificationAsync(
            customerId,
            "announcement",
            request.Subject,
            request.Body,
            NotificationPriority.High,
            NotificationTargetType.MessageThread,
            createdThread.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAdminThread_BroadcastToAll_CreatesThreadForAllCustomers()
    {
        // Arrange
        var customerIds = new List<long> { 101, 102, 103 };
        var request = new CreateAdminThreadRequest(
            CustomerId: null,
            BroadcastToAll: true,
            Subject: "Driftinformation",
            Body: "Underhållsarbete planerat.",
            Category: "System",
            ReplyAllowed: false,
            IsInformationOnly: true);

        _repoMock.Setup(r => r.GetAllCustomerIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerIds);

        var thread1 = new MessageThread(1, request.Subject, true, request.Category);
        var thread2 = new MessageThread(2, request.Subject, true, request.Category);
        var thread3 = new MessageThread(3, request.Subject, true, request.Category);

        _repoMock.Setup(r => r.CreateThreadWithInitialMessageAsync(
                It.IsAny<long>(),
                request.Subject,
                request.Body,
                request.ReplyAllowed,
                request.IsInformationOnly,
                request.Category,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread1);

        // Act
        var count = await _service.CreateAdminThreadAsync(request);

        // Assert
        Assert.Equal(3, count);
        _repoMock.Verify(r => r.CreateThreadWithInitialMessageAsync(
            It.IsAny<long>(),
            request.Subject,
            request.Body,
            request.ReplyAllowed,
            request.IsInformationOnly,
            request.Category,
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task GetNotifications_ReturnsMappedList()
    {
        // Arrange
        const long customerId = 100;
        var notification = new CustomerNotification(
            customerId: customerId,
            type: "support_message",
            title: "Nytt svar",
            body: "Handläggare har svarat.",
            priority: NotificationPriority.Normal,
            targetType: NotificationTargetType.MessageThread,
            targetId: 10);

        var pagedResult = PagedResult<CustomerNotification>.Create([notification], 1, 1, 20);

        _repoMock.Setup(r => r.GetCustomerNotificationsAsync(customerId, false, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _service.GetNotificationsAsync(customerId, new NotificationQueryParameters());

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal("Nytt svar", result.Items.First().Title);
        Assert.False(result.Items.First().IsRead);
    }

    [Fact]
    public async Task CreateSupportTicket_WhenContainingSensitiveData_SanitizesSubjectAndBody()
    {
        // Arrange
        const long customerId = 100;
        var request = new CreateThreadRequest(
            Subject: "Fråga angående 19850512-1234",
            Body: "Mitt personnummer är 19850512-1234",
            Category: "Allmänt");

        var thread = new MessageThread(10, "[Allmänt] Fråga angående 19850512-****");
        _repoMock.Setup(r => r.CreateThreadWithInitialMessageAsync(
            customerId,
            "[Allmänt] Fråga angående 19850512-****",
            "Mitt personnummer är 19850512-****",
            true,
            false,
            "Allmänt",
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(thread);

        _repoMock.Setup(r => r.GetMessagesByThreadIdAsync(thread.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _service.CreateSupportTicketAsync(customerId, request);

        // Assert
        Assert.NotNull(result);
        _repoMock.Verify(r => r.CreateThreadWithInitialMessageAsync(
            customerId,
            "[Allmänt] Fråga angående 19850512-****",
            "Mitt personnummer är 19850512-****",
            true,
            false,
            "Allmänt",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}