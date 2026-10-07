using System.Threading.Channels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Faq.Domain;

namespace Nordiska.Modules.Faq.Application;

/// <summary>
/// Holds FAQ article views in memory until the background worker saves them, same setup as <see cref="FaqSearchLogQueue"/>.
/// </summary>
public sealed class FaqViewLogQueue
{
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DropWarningInterval = TimeSpan.FromMinutes(1);

    private readonly Channel<FaqViewLog> _channel;
    private readonly IMemoryCache _cache;
    private readonly FaqSearchLogOptions _options;
    private readonly ILogger<FaqViewLogQueue> _logger;
    private readonly object _dropLock = new();
    private DateTime _lastDropWarning = DateTime.MinValue;
    private int _droppedSinceWarning;

    public FaqViewLogQueue(
        IMemoryCache cache,
        IOptions<FaqSearchLogOptions> options,
        ILogger<FaqViewLogQueue> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
        _channel = Channel.CreateBounded<FaqViewLog>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public ChannelReader<FaqViewLog> Reader => _channel.Reader;

    public bool TryEnqueue(int faqEntryId, string sessionKey)
    {
        var sessionHash = FaqSessionHash.Compute(_options.Salt, sessionKey);

        // Opening and closing the same article a few times shouldn't make it more popular
        var dedupeKey = $"faqview:{sessionHash}|{faqEntryId}";
        if (_cache.TryGetValue(dedupeKey, out _))
        {
            return false;
        }

        var log = FaqViewLog.Create(faqEntryId, sessionHash, DateTime.UtcNow);

        if (!_channel.Writer.TryWrite(log))
        {
            WarnDropped();
            return false;
        }

        _cache.Set(dedupeKey, true, DedupeWindow);
        return true;
    }

    private void WarnDropped()
    {
        int dropped;
        lock (_dropLock)
        {
            _droppedSinceWarning++;
            var now = DateTime.UtcNow;
            if (now - _lastDropWarning < DropWarningInterval)
            {
                return;
            }

            dropped = _droppedSinceWarning;
            _droppedSinceWarning = 0;
            _lastDropWarning = now;
        }

        _logger.LogWarning("FAQ view log queue is full, dropped {Dropped} view(s) since last warning", dropped);
    }
}
