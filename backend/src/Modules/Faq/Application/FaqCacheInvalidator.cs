using Microsoft.Extensions.Primitives;

namespace Nordiska.Modules.Faq.Application;

/// <summary>
/// Shares one change token between all cached FAQ entries so they can be evicted together
/// when FAQ content is created or deleted.
/// </summary>
public sealed class FaqCacheInvalidator
{
    private readonly object _lock = new();
    private CancellationTokenSource _cts = new();

    /// <summary>
    /// Returns a change token that expires on the next call to <see cref="Invalidate"/>.
    /// </summary>
    public IChangeToken GetChangeToken()
    {
        lock (_lock)
        {
            return new CancellationChangeToken(_cts.Token);
        }
    }

    /// <summary>
    /// Evicts every cached FAQ entry.
    /// </summary>
    public void Invalidate()
    {
        CancellationTokenSource previous;
        lock (_lock)
        {
            previous = _cts;
            _cts = new CancellationTokenSource();
        }

        previous.Cancel();
    }
}
