
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Communication.Domain;

namespace Nordiska.Modules.Inbox.Application;

public interface IInboxRepository
{
    Task<MessageBox?> GetMessageBoxAsync(
        long customerId,
        CancellationToken cancellationToken = default);

    Task<MessageThread?> GetThreadAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken = default);

    Task<MessageThreadState?> GetStateAsync(
        long customerId,
        long threadId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Message>> GetMessagesAsync(
        long threadId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<MessageThread>> GetThreadsAsync(
        long customerId,
        MessageFolder folder,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddMessageBoxAsync(
        MessageBox messageBox,
        CancellationToken cancellationToken = default);

    Task AddThreadAsync(
        MessageThread thread,
        CancellationToken cancellationToken = default);

    Task AddMessageAsync(
        Message message,
        CancellationToken cancellationToken = default);

    Task AddStateAsync(
        MessageThreadState state,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}