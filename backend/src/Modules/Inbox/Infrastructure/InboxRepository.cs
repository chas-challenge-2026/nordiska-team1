using Microsoft.EntityFrameworkCore;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Communication.Domain;
using Nordiska.Modules.Inbox.Application;
using Nordiska.Modules.Inbox.Infrastructure.Db;

namespace Nordiska.Modules.Inbox.Infrastructure;

public sealed class InboxRepository(InboxDbContext db) : IInboxRepository
{
    // Finds the customer's personal message box
    public Task<MessageBox?> GetMessageBoxAsync(
        long customerId,
        CancellationToken cancellationToken = default)
        => db.MessageBoxes
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.CustomerId == customerId, cancellationToken);

    // Loads a thread only when it belongs to the specified customer.
    public Task<MessageThread?> GetThreadAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken = default)
        => (from thread in db.MessageThreads.AsNoTracking()
            join box in db.MessageBoxes.AsNoTracking()
                on thread.MessageBoxId equals box.Id
            where thread.Id == threadId && box.CustomerId == customerId
            select thread).SingleOrDefaultAsync(cancellationToken);

    // Loads the customer's folder and read state for a thread.
    public Task<MessageThreadState?> GetStateAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken = default)
        => db.MessageThreadStates
            .SingleOrDefaultAsync(
                x => x.CustomerId == customerId && x.ThreadId == threadId,
                cancellationToken);

    // Returns non-revoked messages in chronological order.
    public async Task<IReadOnlyList<Message>> GetMessagesAsync(
        long threadId,
        CancellationToken cancellationToken = default)
        => await db.Messages.AsNoTracking()
            .Where(x => x.ThreadId == threadId && x.RevokedAt == null)
            .OrderBy(x => x.SentAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    // Returns the customer's threads for one folder with pagination.
    public async Task<PagedResult<MessageThread>> GetThreadsAsync(
        long customerId,
        MessageFolder folder,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = from thread in db.MessageThreads.AsNoTracking()
                    join box in db.MessageBoxes.AsNoTracking()
                        on thread.MessageBoxId equals box.Id
                    join state in db.MessageThreadStates.AsNoTracking()
                        on thread.Id equals state.ThreadId
                    where box.CustomerId == customerId
                        && state.CustomerId == customerId
                        && state.Folder == folder
                    orderby thread.LastMessageAt descending, thread.Id descending
                    select thread;

        var totalCount = await query.CountAsync(cancellationToken);
        var validPage = Math.Max(page, 1);
        var validPageSize = Math.Clamp(pageSize, 1, 100);
        var items = await query
            .Skip((validPage - 1) * validPageSize)
            .Take(validPageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<MessageThread>.Create(
            items,
            totalCount,
            validPage,
            validPageSize);
    }

    // Adds a new message box to the current unit of work.
    public Task AddMessageBoxAsync(MessageBox messageBox, CancellationToken cancellationToken = default)
    {
        db.MessageBoxes.Add(messageBox);
        return Task.CompletedTask;
    }

    // Adds a new thread to the current unit of work.
    public Task AddThreadAsync(MessageThread thread, CancellationToken cancellationToken = default)
    {
        db.MessageThreads.Add(thread);
        return Task.CompletedTask;
    }

    // Adds a new message to the current unit of work.
    public Task AddMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        db.Messages.Add(message);
        return Task.CompletedTask;
    }

    // Adds customer-specific thread state to the current unit of work.
    public Task AddStateAsync(MessageThreadState state, CancellationToken cancellationToken = default)
    {
        db.MessageThreadStates.Add(state);
        return Task.CompletedTask;
    }

    // Persists all pending Inbox changes
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => db.SaveChangesAsync(cancellationToken);
}