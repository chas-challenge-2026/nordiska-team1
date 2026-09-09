using Nordiska.Modules.Faq.Domain;
using Nordiska.Modules.Faq.Contracts.Responses;
namespace Nordiska.Modules.Faq.Application;
 
public interface IFaqRepository
{
    Task<FaqEntryResponse?> GetByIdAsync(
    int id,
    CancellationToken cancellationToken = default);

    Task<int> CreateAsync(
        FaqEntry entry,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}
public sealed class FaqService(IFaqRepository repository)
{
    public Task<int> CreateAsync(
        string question,
        string answer,
        string? category = null,
        string? keywords = null,
        CancellationToken cancellationToken = default)
    {
        var entry = FaqEntry.Create(
            question,
            answer,
            category,
            keywords);

        return repository.CreateAsync(
            entry,
            cancellationToken);
    }

    public Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return repository.DeleteAsync(
            id,
            cancellationToken);
    }

    public Task<FaqEntryResponse?> GetByIdAsync(
    int id,
    CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);

        return repository.GetByIdAsync(id, cancellationToken);
    }
}