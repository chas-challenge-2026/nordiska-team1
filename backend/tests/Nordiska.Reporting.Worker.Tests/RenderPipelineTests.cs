using Nordiska.Reporting.Worker.AccountStatements;
using Nordiska.Reporting.Worker.TaxReports;

namespace Nordiska.Reporting.Worker.Tests;

public class TaxReportRenderPipelineTests
{
    private readonly FakeTaxReports _reports = new();
    private readonly FakeTaxVerifier _verifier = new();
    private readonly FakePdfGenerator _generator = new();

    private TaxReportRenderPipeline CreatePipeline()
        => new(_reports, _verifier, new AnnualTaxReportNativeMapper(), _generator);

    [Fact]
    public async Task Render_ValidReport_ReturnsNamedPdf()
    {
        var report = ReportingTestData.NewTaxReport(customerId: 7);
        _reports.Report = report;
        var documentId = $"annual_tax_report_{report.Id}";
        _generator.Batch = ReportingTestData.BatchFor(7, documentId, ReportingTestData.ValidPdf);

        var rendered = await CreatePipeline().RenderAsync(report.Id, CancellationToken.None);

        Assert.Equal(report.Id, rendered.TaxReportId);
        Assert.Equal(7, rendered.CustomerId);
        Assert.Equal(documentId, rendered.DocumentId);
        Assert.Equal("arsbesked-2025-1234-567.pdf", rendered.FileName);
        Assert.Equal(ReportingTestData.ValidPdf, rendered.PdfBytes);
        Assert.Equal(1, _verifier.Calls);
        Assert.Contains(documentId, _generator.LastJson);
    }

    [Fact]
    public async Task Render_UnknownReport_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreatePipeline().RenderAsync(99, CancellationToken.None));

        Assert.Contains("99", ex.Message);
        Assert.Equal(0, _generator.Calls);
    }

    [Fact]
    public async Task Render_SnapshotVerificationFails_DoesNotGeneratePdf()
    {
        _reports.Report = ReportingTestData.NewTaxReport();
        _verifier.Throws = new InvalidDataException("tampered");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => CreatePipeline().RenderAsync(1, CancellationToken.None));

        Assert.Equal(0, _generator.Calls);
    }

    [Fact]
    public async Task Render_NativeBatchForWrongCustomer_Throws()
    {
        var report = ReportingTestData.NewTaxReport(customerId: 7);
        _reports.Report = report;
        _generator.Batch = ReportingTestData.BatchFor(8, $"annual_tax_report_{report.Id}", ReportingTestData.ValidPdf);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => CreatePipeline().RenderAsync(report.Id, CancellationToken.None));

        Assert.Contains("wrong customer ID", ex.Message);
    }

    [Fact]
    public async Task Render_NativeBatchMissingExpectedDocument_Throws()
    {
        var report = ReportingTestData.NewTaxReport(customerId: 7);
        _reports.Report = report;
        _generator.Batch = ReportingTestData.BatchFor(7, "something_else", ReportingTestData.ValidPdf);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => CreatePipeline().RenderAsync(report.Id, CancellationToken.None));

        Assert.Contains($"annual_tax_report_{report.Id}", ex.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidPdfs))]
    public async Task Render_NativeReturnsInvalidPdf_Throws(byte[] pdf)
    {
        var report = ReportingTestData.NewTaxReport(customerId: 7);
        _reports.Report = report;
        _generator.Batch = ReportingTestData.BatchFor(7, $"annual_tax_report_{report.Id}", pdf);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => CreatePipeline().RenderAsync(report.Id, CancellationToken.None));

        Assert.Contains("invalid PDF", ex.Message);
    }

    public static TheoryData<byte[]> InvalidPdfs => new()
    {
        Array.Empty<byte>(),
        "%PDF"u8.ToArray(),
        "NOTPDF-1.7 content"u8.ToArray()
    };
}

public class AccountStatementRenderPipelineTests
{
    private readonly FakeAccountStatements _statements = new();
    private readonly FakeStatementVerifier _verifier = new();
    private readonly FakePdfGenerator _generator = new();

    private AccountStatementRenderPipeline CreatePipeline()
        => new(_statements, _verifier, new AccountStatementNativeMapper(), _generator);

    [Fact]
    public async Task Render_ValidStatement_ReturnsNamedPdf()
    {
        var statement = ReportingTestData.NewStatement(customerId: 7);
        _statements.Statement = statement;
        var documentId = $"account_statement_{statement.Id}";
        _generator.Batch = ReportingTestData.BatchFor(7, documentId, ReportingTestData.ValidPdf);

        var rendered = await CreatePipeline().RenderAsync(statement.Id, CancellationToken.None);

        Assert.Equal(statement.Id, rendered.AccountStatementId);
        Assert.Equal(7, rendered.CustomerId);
        Assert.Equal(documentId, rendered.DocumentId);
        Assert.Equal("kontoutdrag-20260101-20260131-1234-567.pdf", rendered.FileName);
        Assert.Equal(ReportingTestData.ValidPdf, rendered.PdfBytes);
        Assert.Equal(1, _verifier.Calls);
        Assert.Contains(documentId, _generator.LastJson);
    }

    [Fact]
    public async Task Render_UnknownStatement_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreatePipeline().RenderAsync(99, CancellationToken.None));

        Assert.Contains("99", ex.Message);
        Assert.Equal(0, _generator.Calls);
    }

    [Fact]
    public async Task Render_SnapshotVerificationFails_DoesNotGeneratePdf()
    {
        _statements.Statement = ReportingTestData.NewStatement();
        _verifier.Throws = new InvalidDataException("tampered");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => CreatePipeline().RenderAsync(1, CancellationToken.None));

        Assert.Equal(0, _generator.Calls);
    }

    [Fact]
    public async Task Render_NativeBatchForWrongCustomer_Throws()
    {
        var statement = ReportingTestData.NewStatement(customerId: 7);
        _statements.Statement = statement;
        _generator.Batch = ReportingTestData.BatchFor(8, $"account_statement_{statement.Id}", ReportingTestData.ValidPdf);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => CreatePipeline().RenderAsync(statement.Id, CancellationToken.None));

        Assert.Contains("wrong customer ID", ex.Message);
    }

    [Fact]
    public async Task Render_NativeBatchMissingExpectedDocument_Throws()
    {
        var statement = ReportingTestData.NewStatement(customerId: 7);
        _statements.Statement = statement;
        _generator.Batch = ReportingTestData.BatchFor(7, "something_else", ReportingTestData.ValidPdf);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => CreatePipeline().RenderAsync(statement.Id, CancellationToken.None));

        Assert.Contains($"account_statement_{statement.Id}", ex.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidPdfs))]
    public async Task Render_NativeReturnsInvalidPdf_Throws(byte[] pdf)
    {
        var statement = ReportingTestData.NewStatement(customerId: 7);
        _statements.Statement = statement;
        _generator.Batch = ReportingTestData.BatchFor(7, $"account_statement_{statement.Id}", pdf);

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            () => CreatePipeline().RenderAsync(statement.Id, CancellationToken.None));

        Assert.Contains("invalid PDF", ex.Message);
    }

    public static TheoryData<byte[]> InvalidPdfs => new()
    {
        Array.Empty<byte>(),
        "%PDF"u8.ToArray(),
        "NOTPDF-1.7 content"u8.ToArray()
    };
}
