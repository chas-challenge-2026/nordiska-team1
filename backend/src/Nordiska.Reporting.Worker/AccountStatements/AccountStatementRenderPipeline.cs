using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.PdfGeneration;

namespace Nordiska.Reporting.Worker.AccountStatements;

public sealed record RenderedAccountStatement(
    long AccountStatementId,
    long CustomerId,
    string DocumentId,
    string FileName,
    byte[] PdfBytes);

public interface IAccountStatementRenderPipeline
{
    Task<RenderedAccountStatement> RenderAsync(
        long accountStatementId,
        CancellationToken cancellationToken);
}

public sealed class AccountStatementRenderPipeline
    : IAccountStatementRenderPipeline
{
    private readonly IAccountStatementRepository _statements;
    private readonly IAccountStatementSnapshotVerifier _verifier;
    private readonly AccountStatementNativeMapper _mapper;
    private readonly IPdfBatchGenerator _generator;

    public AccountStatementRenderPipeline(
        IAccountStatementRepository statements,
        IAccountStatementSnapshotVerifier verifier,
        AccountStatementNativeMapper mapper,
        IPdfBatchGenerator generator)
    {
        _statements = statements;
        _verifier = verifier;
        _mapper = mapper;
        _generator = generator;
    }

    public async Task<RenderedAccountStatement> RenderAsync(
        long accountStatementId,
        CancellationToken cancellationToken)
    {
        AccountStatement statement =
            await _statements.GetByIdAsync(
                accountStatementId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Account statement {accountStatementId} was not found.");

        _verifier.Verify(statement);

        string json = _mapper.CreateJson(statement);

        GeneratedPdfBatch batch =
            _generator.GeneratePdfBatch(json);

        ulong expectedCustomerId =
            checked((ulong)statement.CustomerId);

        if (batch.CustomerId != expectedCustomerId)
        {
            throw new InvalidDataException(
                "Native PDF batch returned the wrong customer ID.");
        }

        string expectedDocumentId =
            $"account_statement_{statement.Id}";

        if (!batch.Documents.TryGetValue(
                expectedDocumentId,
                out byte[]? pdfBytes))
        {
            throw new InvalidDataException(
                $"Native PDF batch did not contain " +
                $"{expectedDocumentId}.");
        }

        if (pdfBytes.Length < 5 ||
            !pdfBytes.AsSpan().StartsWith("%PDF-"u8))
        {
            throw new InvalidDataException(
                "Native generator returned invalid PDF bytes.");
        }

        return new RenderedAccountStatement(
            statement.Id,
            statement.CustomerId,
            expectedDocumentId,
            $"kontoutdrag-{statement.PeriodStart:yyyyMMdd}-" +
            $"{statement.PeriodEnd:yyyyMMdd}-" +
            $"{statement.AccountNumber}.pdf",
            pdfBytes);
    }
}
