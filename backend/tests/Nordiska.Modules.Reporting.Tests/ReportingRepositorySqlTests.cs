using Nordiska.Modules.Reporting.Infrastructure;

namespace Nordiska.Modules.Reporting.Tests;

public sealed class ReportingRepositorySqlTests
{
    [Fact]
    public void TaxReportClaim_UsesOneAtomicUpdateReturningStatement()
    {
        AssertAtomicClaim(
            TaxReportJobRepository.ClaimNextSql,
            "tax_report_jobs");
    }

    [Fact]
    public void AccountStatementClaim_UsesOneAtomicUpdateReturningStatement()
    {
        AssertAtomicClaim(
            AccountStatementJobRepository.ClaimNextSql,
            "account_statement_jobs");
    }

    [Fact]
    public void TaxReportCompletion_UsesOneStatementForDocumentLinkAndJob()
    {
        AssertAtomicCompletion(
            TaxReportCompletionRepository.CompleteSql,
            "tax_report_documents",
            "tax_report_jobs");
    }

    [Fact]
    public void AccountStatementCompletion_UsesOneStatementForDocumentLinkAndJob()
    {
        AssertAtomicCompletion(
            AccountStatementCompletionRepository.CompleteSql,
            "account_statement_documents",
            "account_statement_jobs");
    }

    private static void AssertAtomicClaim(
        string sql,
        string jobTable)
    {
        Assert.Contains(
            "FOR UPDATE SKIP LOCKED",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"UPDATE reporting.{jobTable}",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "RETURNING",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, sql.Count(character => character == ';'));
    }

    private static void AssertAtomicCompletion(
        string sql,
        string linkTable,
        string jobTable)
    {
        sql = sql.ReplaceLineEndings("\n");

        Assert.Contains(
            "INSERT INTO reporting.generated_documents",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"INSERT INTO reporting.{linkTable}",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            $"UPDATE reporting.{jobTable}",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "ON CONFLICT",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "ON CONFLICT (\"StorageKey\") DO NOTHING",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "ON CONFLICT (\"StorageKey\") DO UPDATE",
            sql,
            StringComparison.OrdinalIgnoreCase);
        int documentLookup = sql.IndexOf(
            "WHERE document.\"StorageKey\" = @StorageKey",
            StringComparison.OrdinalIgnoreCase);
        int ownershipGate = sql.IndexOf(
            "FROM owned_job",
            documentLookup,
            StringComparison.OrdinalIgnoreCase);

        Assert.True(documentLookup >= 0);
        Assert.InRange(
            ownershipGate - documentLookup,
            1,
            200);
        Assert.Equal(1, sql.Count(character => character == ';'));
    }
}
