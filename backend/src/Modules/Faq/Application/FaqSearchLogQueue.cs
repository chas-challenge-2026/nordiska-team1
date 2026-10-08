using System.Threading.Channels;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Faq.Domain;

namespace Nordiska.Modules.Faq.Application;

/// <summary>
/// Holds FAQ searches in memory until the background worker saves them, so logging never slows down the search request.
/// </summary>
public sealed class FaqSearchLogQueue
{
    public const int MaxQueryLength = 200;

    private static readonly TimeSpan DedupeWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DropWarningInterval = TimeSpan.FromMinutes(1);

    private readonly Channel<FaqSearchLog> _channel;
    private readonly IMemoryCache _cache;
    private readonly FaqSearchLogOptions _options;
    private readonly ILogger<FaqSearchLogQueue> _logger;
    private readonly object _dropLock = new();
    private DateTime _lastDropWarning = DateTime.MinValue;
    private int _droppedSinceWarning;

    public FaqSearchLogQueue(
        IMemoryCache cache,
        IOptions<FaqSearchLogOptions> options,
        ILogger<FaqSearchLogQueue> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
        _channel = Channel.CreateBounded<FaqSearchLog>(new BoundedChannelOptions(_options.QueueCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public ChannelReader<FaqSearchLog> Reader => _channel.Reader;

    public bool TryEnqueue(string query, string language, int resultCount, string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        // Masked before anything else touches it, so personal data never reaches the channel or the database
        var masked = Truncate(FaqSearchQuery.Mask(query.Trim()));
        var normalized = Truncate(FaqSearchQuery.Normalize(masked));
        var lang = language.Trim().ToLowerInvariant();
        var sessionHash = FaqSessionHash.Compute(_options.Salt, sessionKey);

        var dedupeKey = $"faqsearch:{sessionHash}|{lang}|{normalized}";
        if (_cache.TryGetValue(dedupeKey, out _))
        {
            return false;
        }

        var log = FaqSearchLog.Create(masked, normalized, lang, resultCount, sessionHash, DateTime.UtcNow);

        // FullMode Wait makes TryWrite return false instead of blocking when the queue is full
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

        _logger.LogWarning("FAQ search log queue is full, dropped {Dropped} search(es) since last warning", dropped);
    }

    private static string Truncate(string value)
        => value.Length > MaxQueryLength ? value[..MaxQueryLength] : value;
}
