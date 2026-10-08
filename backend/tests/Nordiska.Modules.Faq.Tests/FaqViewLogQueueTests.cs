using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nordiska.Modules.Faq.Application;

namespace Nordiska.Modules.Faq.Tests;

public class FaqViewLogQueueTests
{
    private static FaqViewLogQueue CreateQueue(int capacity = 1000, string salt = "test-salt")
        => new(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new FaqSearchLogOptions { Salt = salt, QueueCapacity = capacity }),
            NullLogger<FaqViewLogQueue>.Instance);

    [Fact]
    public void TryEnqueue_SameArticleSameSession_IsOnlyQueuedOnce()
    {
        var queue = CreateQueue();

        Assert.True(queue.TryEnqueue(1, "1.2.3.4|firefox"));
        Assert.False(queue.TryEnqueue(1, "1.2.3.4|firefox"));

        Assert.Equal(1, queue.Reader.Count);
    }

    [Fact]
    public void TryEnqueue_OtherArticleOrSession_IsQueuedAgain()
    {
        var queue = CreateQueue();

        queue.TryEnqueue(1, "1.2.3.4|firefox");
        queue.TryEnqueue(2, "1.2.3.4|firefox");
        queue.TryEnqueue(1, "5.6.7.8|chrome");

        Assert.Equal(3, queue.Reader.Count);
    }

    [Fact]
    public void TryEnqueue_SessionIsStoredAsSameHashAsSearchLog()
    {
        var queue = CreateQueue();

        queue.TryEnqueue(1, "1.2.3.4|firefox");

        Assert.True(queue.Reader.TryRead(out var log));
        Assert.Equal(1, log!.FaqEntryId);
        Assert.Equal(64, log.SessionHash.Length);
        Assert.DoesNotContain("1.2.3.4", log.SessionHash);
        Assert.Equal(FaqSessionHash.Compute("test-salt", "1.2.3.4|firefox"), log.SessionHash);
    }

    [Fact]
    public void TryEnqueue_FullQueue_DropsWithoutBlocking()
    {
        var queue = CreateQueue(capacity: 1);

        Assert.True(queue.TryEnqueue(1, "session-1"));
        Assert.False(queue.TryEnqueue(1, "session-2"));

        Assert.Equal(1, queue.Reader.Count);
    }
}
