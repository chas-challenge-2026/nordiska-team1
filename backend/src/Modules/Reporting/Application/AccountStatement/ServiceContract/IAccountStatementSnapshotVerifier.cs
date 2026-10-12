using System.Security.Cryptography;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.Modules.Reporting.Application;

public interface IAccountStatementSnapshotVerifier
{
    void Verify(AccountStatement statement);
}

public sealed class AccountStatementSnapshotVerifier
    : IAccountStatementSnapshotVerifier
{
    private readonly IAccountStatementPayloadHasher _hasher;

    public AccountStatementSnapshotVerifier(
        IAccountStatementPayloadHasher hasher)
    {
        _hasher = hasher;
    }

    public void Verify(AccountStatement statement)
    {
        string calculatedHash =
            _hasher.Compute(
                AccountStatementHashPayload.From(statement));

        byte[] expected;
        byte[] calculated;

        try
        {
            expected = Convert.FromHexString(statement.PayloadHash);
            calculated = Convert.FromHexString(calculatedHash);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException(
                "Account statement contains an invalid payload hash.",
                exception);
        }

        if (expected.Length != calculated.Length ||
            !CryptographicOperations.FixedTimeEquals(
                expected,
                calculated))
        {
            throw new InvalidDataException(
                "Account statement payload hash verification failed.");
        }
    }
}
