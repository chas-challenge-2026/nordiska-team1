using Nordiska.BuildingBlocks.Database;
using Nordiska.BuildingBlocks.Database.Errors;
using Nordiska.Modules.Communication.Domain;
using Nordiska.Modules.Inbox.Application;

namespace Nordiska.Modules.Inbox.Infrastructure;

public sealed class InboxService(IInboxRepository repository) : IInboxService
{
    // Lists the customer's threads in the selected folder
    public async Task<PagedResult<InboxThreadResponse>> GetThreadsAsync(
        long customerId,
        InboxThreadQuery query,
        CancellationToken cancellationToken = default)
    {
        // Validate the query parameters 
        ArgumentNullException.ThrowIfNull(query);

       // query the repository for the customer's threads in the specified folder with pagination
        var paged = await repository.GetThreadsAsync(
            customerId,
            query.Folder,
            query.Page,
            query.PageSize,
            cancellationToken);
        
        // Convert the threads to response objects
        var responses = new List<InboxThreadResponse>(paged.Items.Count);
        foreach (var thread in paged.Items)
        {
            responses.Add(await ToResponseAsync(customerId, thread, cancellationToken));
        }
        
        // Return the paged result with the responses and pagination metadata
        return new PagedResult<InboxThreadResponse>(
            responses,
            paged.TotalCount,
            paged.Page,
            paged.PageSize,
            paged.TotalPages,
            paged.HasNextPage,
            paged.HasPreviousPage);
    }

    // Loads one customer-owned thread and its visible messages.
    public async Task<InboxThreadResponse> GetThreadAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken = default)
    {
        var thread = await GetOwnedThreadAsync(customerId, threadId, cancellationToken);
        return await ToResponseAsync(customerId, thread, cancellationToken);
    }

    // Creates a thread, its first customer message, and initial state
    public async Task<InboxThreadResponse> CreateThreadAsync(
        long customerId,
        CreateInboxThreadRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate the request object
        ArgumentNullException.ThrowIfNull(request);
        
        // Trim the subject and body to remove leading and trailing whitespace
        var subject = request.Subject?.Trim();
        var body = request.Body?.Trim();

        // Throw an exception if subject or body is null, empty, or whitespace
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Subject is required.", nameof(request));
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Body is required.", nameof(request));
        }
        // Ensure the customer has a message box, creating one if necessary
        var box = await repository.GetMessageBoxAsync(customerId, cancellationToken);
        if (box is null)
        {
            box = new MessageBox(customerId);
            await repository.AddMessageBoxAsync(box, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }
        // Create a new thread with the subject and associate it with the customer's message box
        var thread = new MessageThread(box.Id, subject);
        await repository.AddThreadAsync(thread, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        // Create the first message in the thread, marking it as sent by the customer
        var message = new Message(
            thread.Id,
            MessageSenderType.Customer,
            body,
            replyAllowed: true,
            senderCustomerId: customerId);
        var state = new MessageThreadState(thread.Id, customerId, MessageFolder.Sent);
        
        // Add the message and state to the repository and save changes
        await repository.AddMessageAsync(message, cancellationToken);
        await repository.AddStateAsync(state, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return await ToResponseAsync(customerId, thread, cancellationToken);
    }

    // Marks a customer-owned thread as read.
    public async Task MarkAsReadAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken = default)
    {
        await GetOwnedThreadAsync(customerId, threadId, cancellationToken);
        var state = await repository.GetStateAsync(customerId, threadId, cancellationToken)
            ?? throw new NotFoundException("Message thread state not found.");

        state.MarkAsRead();
        await repository.SaveChangesAsync(cancellationToken);
    }

    // Moves a customer-owned thread to the archive folder
    public async Task ArchiveAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken = default)
    {
        await GetOwnedThreadAsync(customerId, threadId, cancellationToken);
        var state = await repository.GetStateAsync(customerId, threadId, cancellationToken)
            ?? throw new NotFoundException("Message thread state not found.");

        state.Archive();
        await repository.SaveChangesAsync(cancellationToken);
    }

    //Loads a thread or hides whether it exists for another customer
    private async Task<MessageThread> GetOwnedThreadAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken)
        => await repository.GetThreadAsync(customerId, threadId, cancellationToken)
            ?? throw new NotFoundException("Message thread not found.");

    // Combines thread, state, and messages into the API response
    private async Task<InboxThreadResponse> ToResponseAsync(
        long customerId,
        MessageThread thread,
        CancellationToken cancellationToken)
    {
        // Load the thread state for the customer, throwing an exception if not found
        var state = await repository.GetStateAsync(customerId, thread.Id, cancellationToken)
            ?? throw new NotFoundException("Message thread state not found.");
        var messages = await repository.GetMessagesAsync(thread.Id, cancellationToken);

        // Create and return the response object with thread details, state, and messages
        return new InboxThreadResponse(
            thread.Id,
            thread.Subject,
            thread.Status,
            state.Folder,
            state.IsRead,
            thread.CreatedAt,
            thread.LastMessageAt,
            messages.Select(message => new InboxMessageResponse(
                message.Id,
                message.SenderType,
                message.Body,
                message.ReplyAllowed,
                message.SentAt,
                message.RevokedAt)).ToList());
    }
}