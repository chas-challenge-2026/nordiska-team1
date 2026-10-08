using Nordiska.Modules.CustomerCenter.Domain;
using Nordiska.Modules.Inbox.Application;

namespace Nordiska.Modules.Inbox.Tests;

public sealed class FeedItemIdentityTests
{
    [Theory]
    [InlineData("thread-42", FeedItemType.Message, 42)]
    [InlineData("notification-7", FeedItemType.Notification, 7)]
    [InlineData("document-19", FeedItemType.Document, 19)]
    [InlineData("term-3", FeedItemType.Terms, 3)]
    public void TryParse_CanonicalIdentity_ReturnsTypeAndSourceId(
        string value,
        FeedItemType expectedType,
        long expectedSourceId)
    {
        var parsed = FeedItemIdentity.TryParse(value, out var itemType, out var sourceId);

        Assert.True(parsed);
        Assert.Equal(expectedType, itemType);
        Assert.Equal(expectedSourceId, sourceId);
    }

    [Theory]
    [InlineData(FeedItemType.Message, 42, "thread-42")]
    [InlineData(FeedItemType.Notification, 7, "notification-7")]
    [InlineData(FeedItemType.Document, 19, "document-19")]
    [InlineData(FeedItemType.Terms, 3, "term-3")]
    public void Format_SupportedType_ReturnsCanonicalIdentity(
        FeedItemType itemType,
        long sourceId,
        string expected)
    {
        Assert.Equal(expected, FeedItemIdentity.Format(itemType, sourceId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("thread")]
    [InlineData("thread-")]
    [InlineData("thread-0")]
    [InlineData("thread--1")]
    [InlineData("threads-1")]
    [InlineData("message-1")]
    [InlineData("loan-1")]
    [InlineData("document-not-a-number")]
    public void TryParse_InvalidIdentity_ReturnsFalse(string value)
    {
        Assert.False(FeedItemIdentity.TryParse(value, out _, out _));
    }

    [Fact]
    public void Format_NonPositiveSourceId_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FeedItemIdentity.Format(FeedItemType.Message, 0));
    }

    [Fact]
    public void Format_UnsupportedLoanType_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FeedItemIdentity.Format(FeedItemType.Loan, 1));
    }
}
