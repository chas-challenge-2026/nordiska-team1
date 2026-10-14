using System.Security.Cryptography;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Application;

public interface ITaxReportSnapshotVerifier
{
    void Verify(TaxReport report);
}

public sealed class TaxReportSnapshotVerifier
    : ITaxReportSnapshotVerifier
{
    private readonly ITaxReportPayloadHasher _hasher;

    public TaxReportSnapshotVerifier(
        ITaxReportPayloadHasher hasher)
    {
        _hasher = hasher;
    }

    public void Verify(TaxReport report)
    {
        string calculatedHash =
            _hasher.Compute(
                TaxReportHashPayload.From(report));

        byte[] expected;
        byte[] calculated;

        try
        {
            expected =
                Convert.FromHexString(report.PayloadHash);

            calculated =
                Convert.FromHexString(calculatedHash);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException(
                "Tax report contains an invalid payload hash.",
                exception);
        }

        if (expected.Length != calculated.Length ||
            !CryptographicOperations.FixedTimeEquals(
                expected,
                calculated))
        {
            throw new InvalidDataException(
                "Tax report payload hash verification failed.");
        }
    }
}