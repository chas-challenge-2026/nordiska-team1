using System.Globalization;
using Nordiska.Modules.CustomerCenter.Domain;

namespace Nordiska.Modules.Inbox.Application;

public static class FeedItemIdentity
{
    public static string Format(FeedItemType itemType, long sourceId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceId);

        var prefix = itemType switch
        {
            FeedItemType.Message => "thread",
            FeedItemType.Notification => "notification",
            FeedItemType.Document => "document",
            FeedItemType.Terms => "term",
            _ => throw new ArgumentOutOfRangeException(nameof(itemType), itemType, "Unsupported feed item type.")
        };

        return $"{prefix}-{sourceId.ToString(CultureInfo.InvariantCulture)}";
    }

    public static bool TryParse(string? value, out FeedItemType itemType, out long sourceId)
    {
        itemType = default;
        sourceId = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var separatorIndex = value.LastIndexOf('-');
        if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
        {
            return false;
        }

        var prefix = value[..separatorIndex];
        if (!long.TryParse(value[(separatorIndex + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out sourceId) || sourceId <= 0)
        {
            sourceId = default;
            return false;
        }

        itemType = prefix switch
        {
            "thread" => FeedItemType.Message,
            "notification" => FeedItemType.Notification,
            "document" => FeedItemType.Document,
            "term" => FeedItemType.Terms,
            _ => default
        };

        if (itemType == default)
        {
            sourceId = default;
            return false;
        }

        return true;
    }
}
