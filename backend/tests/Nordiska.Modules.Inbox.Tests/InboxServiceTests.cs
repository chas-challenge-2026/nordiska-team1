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
            new StaffReplyRequestValidator());
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
}