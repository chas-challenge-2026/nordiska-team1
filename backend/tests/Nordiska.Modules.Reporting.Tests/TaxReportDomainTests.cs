using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Tests;

public sealed class TaxReportDomainTests
{
    [Fact]
    public void MarkReported_RecordsAuthorityDetails()
    {
        var report = CreateReport();
        var reportedAt = new DateTimeOffset(
            2026,
            2,
            15,
            10,
            30,
            0,
            TimeSpan.Zero);

        report.MarkReported(
            "Skatteverket",
            "KU20-12345",
            reportedAt);

        Assert.Equal("Reported", report.ReportingStatus);
        Assert.Equal(reportedAt, report.ReportedAt);
        Assert.Equal("Skatteverket", report.ReportingAuthority);
        Assert.Equal("KU20-12345", report.AuthorityReference);
    }

    private static TaxReport CreateReport()
    {
        return TaxReport.Create(
            customerId: 1,
            accountId: 2,
            taxYear: 2025,
            totalInterestMinor: 43_750,
            taxDeductedMinor: 13_125,
            currency: "SEK",
            customerName: "Anna Lindqvist",
            accountNumber: "NKM-10001",
            accountName: "Sparkonto",
            createdAt: new DateTimeOffset(
                2026,
                2,
                1,
                10,
                0,
                0,
                TimeSpan.Zero),
            schemaVersion: "1.0",
            payloadHash: new string('a', 64));
    }
}
