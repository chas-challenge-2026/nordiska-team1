using Moq;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Xunit;

namespace Nordiska.Modules.Reporting.Tests;

public sealed class SnapshotVerifierTests
{
    [Fact]
    public void TaxReportVerifier_AcceptsMatchingHash()
    {
        TaxReport report = CreateTaxReport("aabb");
        var hasher = new Mock<ITaxReportPayloadHasher>();
        hasher.Setup(h => h.Compute(It.IsAny<TaxReportHashPayload>())).Returns("aabb");

        new TaxReportSnapshotVerifier(hasher.Object).Verify(report);
    }

    [Fact]
    public void TaxReportVerifier_RejectsInvalidOrMismatchedHash()
    {
        var invalid = new TaxReportSnapshotVerifier(
            Mock.Of<ITaxReportPayloadHasher>(h => h.Compute(It.IsAny<TaxReportHashPayload>()) == "zz"));
        Assert.Throws<InvalidDataException>(() => invalid.Verify(CreateTaxReport("zz")));

        var mismatch = new TaxReportSnapshotVerifier(
            Mock.Of<ITaxReportPayloadHasher>(h => h.Compute(It.IsAny<TaxReportHashPayload>()) == "aabb"));
        Assert.Throws<InvalidDataException>(() => mismatch.Verify(CreateTaxReport("ccdd")));
    }

    [Fact]
    public void AccountStatementVerifier_AcceptsMatchingHash()
    {
        AccountStatement statement = CreateAccountStatement("aabb");
        var hasher = new Mock<IAccountStatementPayloadHasher>();
        hasher.Setup(h => h.Compute(It.IsAny<AccountStatementHashPayload>())).Returns("aabb");

        new AccountStatementSnapshotVerifier(hasher.Object).Verify(statement);
    }

    [Fact]
    public void AccountStatementVerifier_RejectsInvalidOrMismatchedHash()
    {
        var invalid = new AccountStatementSnapshotVerifier(
            Mock.Of<IAccountStatementPayloadHasher>(h => h.Compute(It.IsAny<AccountStatementHashPayload>()) == "zz"));
        Assert.Throws<InvalidDataException>(() => invalid.Verify(CreateAccountStatement("zz")));

        var mismatch = new AccountStatementSnapshotVerifier(
            Mock.Of<IAccountStatementPayloadHasher>(h => h.Compute(It.IsAny<AccountStatementHashPayload>()) == "aabb"));
        Assert.Throws<InvalidDataException>(() => mismatch.Verify(CreateAccountStatement("ccdd")));
    }

    private static TaxReport CreateTaxReport(string hash) => TaxReport.Create(
        1, null, 2025, 100, 30, "SEK", "Customer", "ALL", "Skatteunderlag 2025",
        DateTimeOffset.UtcNow, "1.0", hash);

    private static AccountStatement CreateAccountStatement(string hash) => AccountStatement.Create(
        1, 2, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31),
        "Customer", "ACC", "Savings", "SEK", 0, 0, DateTimeOffset.UtcNow, "1.0", hash, []);
}
