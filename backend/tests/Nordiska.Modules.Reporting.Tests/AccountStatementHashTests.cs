using Nordiska.Modules.Reporting.Application;
using Xunit;

namespace Nordiska.Modules.Reporting.Tests;

public sealed class AccountStatementHashTests
{
    [Fact]
    public void Compute_SamePayload_IsDeterministic()
    {
        var payload = new AccountStatementHashPayload(
            1, 2, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31),
            "Customer", "ACC-2", "Savings", "SEK", 100, 200,
            DateTimeOffset.UtcNow, "1.0",
            [new AccountStatementEntryHashPayload(1, 9, DateTimeOffset.UtcNow, "deposit", "Deposit", 100, 200)]);

        var hasher = new AccountStatementPayloadHasher();

        Assert.Equal(hasher.Compute(payload), hasher.Compute(payload));
        Assert.Equal(64, hasher.Compute(payload).Length);
    }

    [Fact]
    public void Compute_ChangingEntryChangesHash()
    {
        var payload = new AccountStatementHashPayload(
            1, 2, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31),
            "Customer", "ACC-2", "Savings", "SEK", 100, 200,
            DateTimeOffset.UtcNow, "1.0", []);
        var changed = payload with { ClosingBalanceMinor = 201 };

        var hasher = new AccountStatementPayloadHasher();

        Assert.NotEqual(hasher.Compute(payload), hasher.Compute(changed));
    }
}
