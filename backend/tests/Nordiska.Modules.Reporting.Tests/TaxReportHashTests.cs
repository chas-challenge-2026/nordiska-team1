using Nordiska.Modules.Reporting.Application;

namespace Nordiska.Reporting.Tests;

public sealed class TaxReportHashTests
{
    [Fact]
    public void Compute_SameSnapshot_ReturnsExpectedHash()
    {
        var payload = new TaxReportHashPayload(
            CustomerId: 1,
            AccountId: 2,
            TaxYear: 2025,
            TotalInterestMinor: 12345,
            TaxDeductedMinor: 3704,
            Currency: "SEK",
            CustomerName: "Anna Andersson",
            AccountNumber: "NOR-100001",
            AccountName: "Sparkonto",
            CreatedAt: new DateTimeOffset(
                2026,
                1,
                2,
                3,
                4,
                5,
                TimeSpan.Zero),
            SchemaVersion: "1.0");

        var hasher = new TaxReportPayloadHasher();

        string result = hasher.Compute(payload);

        Assert.Equal(
            "346b312fb7d7fa1abd1e0f209809abd22a633b69e830a33274b232acd35b521a",
            result);
    }

    [Fact]
    public void Compute_PostgresTruncatesSubMicrosecondTicks_ReturnsSameHash()
    {
        DateTimeOffset originalCreatedAt =
            new DateTimeOffset(
                2026,
                1,
                2,
                3,
                4,
                5,
                TimeSpan.Zero)
            .AddTicks(7);

        long postgresTicks =
            originalCreatedAt.UtcDateTime.Ticks -
            originalCreatedAt.UtcDateTime.Ticks % 10;

        DateTimeOffset postgresCreatedAt =
            new(postgresTicks, TimeSpan.Zero);

        var beforeDatabase = new TaxReportHashPayload(
            CustomerId: 1,
            AccountId: 2,
            TaxYear: 2025,
            TotalInterestMinor: 12345,
            TaxDeductedMinor: 3704,
            Currency: "SEK",
            CustomerName: "Anna Andersson",
            AccountNumber: "NOR-100001",
            AccountName: "Sparkonto",
            CreatedAt: originalCreatedAt,
            SchemaVersion: "1.0");

        TaxReportHashPayload afterDatabase =
            beforeDatabase with
            {
                CreatedAt = postgresCreatedAt
            };

        var hasher = new TaxReportPayloadHasher();

        Assert.Equal(
            hasher.Compute(beforeDatabase),
            hasher.Compute(afterDatabase));
    }
}
