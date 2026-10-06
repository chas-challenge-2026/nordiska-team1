using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Faq.Application;

namespace Nordiska.Modules.Faq.Tests;

public class FaqSearchLogQueueTests
{
    private static FaqSearchLogQueue CreateQueue(int capacity = 1000, string salt = "test-salt")
        => new(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new FaqSearchLogOptions { Salt = salt, QueueCapacity = capacity }),
            NullLogger<FaqSearchLogQueue>.Instance);

    [Fact]
    public void TryEnqueue_SameSearchSameSession_IsOnlyQueuedOnce()
    {
        var queue = CreateQueue();

        Assert.True(queue.TryEnqueue("ränta", "sv", 3, "1.2.3.4|firefox"));
        Assert.False(queue.TryEnqueue("  Ränta ", "sv", 3, "1.2.3.4|firefox"));

        Assert.Equal(1, queue.Reader.Count);
    }

    [Fact]
    public void TryEnqueue_OtherSessionOrLanguage_IsQueuedAgain()
    {
        var queue = CreateQueue();

        queue.TryEnqueue("ränta", "sv", 3, "1.2.3.4|firefox");
        queue.TryEnqueue("ränta", "sv", 3, "5.6.7.8|chrome");
        queue.TryEnqueue("ränta", "en", 0, "1.2.3.4|firefox");

        Assert.Equal(3, queue.Reader.Count);
    }

    [Fact]
    public void TryEnqueue_MasksPersonalDataBeforeQueueing()
    {
        var queue = CreateQueue();

        queue.TryEnqueue("Lån 199001011234 anna@example.se", "sv", 0, "1.2.3.4|firefox");

        Assert.True(queue.Reader.TryRead(out var log));
        Assert.Equal("Lån [personnummer] [email]", log!.Query);
        Assert.Equal("lån [personnummer] [email]", log.NormalizedQuery);
        Assert.Equal(0, log.ResultCount);
    }

    [Fact]
    public void TryEnqueue_SessionIsStoredAsSaltedHash()
    {
        var queue = CreateQueue();
        var otherSalt = CreateQueue(salt: "other-salt");

        queue.TryEnqueue("ränta", "sv", 1, "1.2.3.4|firefox");
        otherSalt.TryEnqueue("ränta", "sv", 1, "1.2.3.4|firefox");

        queue.Reader.TryRead(out var log);
        otherSalt.Reader.TryRead(out var otherLog);

        Assert.Equal(64, log!.SessionHash.Length);
        Assert.DoesNotContain("1.2.3.4", log.SessionHash);
        Assert.NotEqual(log.SessionHash, otherLog!.SessionHash);
    }

    [Fact]
    public void TryEnqueue_FullQueue_DropsWithoutBlocking()
    {
        var queue = CreateQueue(capacity: 1);

        Assert.True(queue.TryEnqueue("ränta", "sv", 1, "session-1"));
        Assert.False(queue.TryEnqueue("ränta", "sv", 1, "session-2"));

        Assert.Equal(1, queue.Reader.Count);
    }

    [Fact]
    public void TryEnqueue_LongQuery_IsCutToMaxLength()
    {
        var queue = CreateQueue();

        queue.TryEnqueue(new string('a', 500), "sv", 0, "session-1");

        queue.Reader.TryRead(out var log);
        Assert.Equal(FaqSearchLogQueue.MaxQueryLength, log!.Query.Length);
    }
}
