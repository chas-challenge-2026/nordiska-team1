namespace Nordiska.Modules.Reporting.Domain;

public static class AccountStatementJobStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public sealed class AccountStatementJob
{
    private AccountStatementJob()
    {
    }

    private AccountStatementJob(
        long accountStatementId,
        DateTimeOffset createdAt)
    {
        AccountStatementId = accountStatementId;
        Status = AccountStatementJobStatuses.Pending;
        CreatedAt = createdAt;
        AvailableAt = createdAt;
    }

    public long Id { get; private set; }

    public long AccountStatementId { get; private set; }

    public string Status { get; private set; } = null!;

    public int AttemptCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset AvailableAt { get; private set; }

    public string? LockedBy { get; private set; }

    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? LastError { get; private set; }

    public static AccountStatementJob Create(
        long accountStatementId,
        DateTimeOffset createdAt)
    {
        return new AccountStatementJob(
            accountStatementId,
            createdAt);
    }

    public void Claim(
        string workerId,
        DateTimeOffset now,
        TimeSpan leaseDuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(leaseDuration));
        }

        bool pendingAndAvailable =
            Status == AccountStatementJobStatuses.Pending &&
            AvailableAt <= now;

        bool expiredLease =
            Status == AccountStatementJobStatuses.Processing &&
            LeaseExpiresAt is { } leaseExpiresAt &&
            leaseExpiresAt <= now;

        if (!pendingAndAvailable && !expiredLease)
        {
            throw new InvalidOperationException(
                "Account statement job is not available for processing.");
        }

        Status = AccountStatementJobStatuses.Processing;
        AttemptCount++;
        LockedBy = workerId;
        LeaseExpiresAt = now.Add(leaseDuration);
        StartedAt ??= now;
        CompletedAt = null;
        LastError = null;
    }

    public void MarkCompleted(
        string workerId,
        DateTimeOffset completedAt)
    {
        EnsureOwnedBy(workerId);
        Status = AccountStatementJobStatuses.Completed;
        CompletedAt = completedAt;
        LockedBy = null;
        LeaseExpiresAt = null;
        LastError = null;
    }

    public void MarkFailed(
        string workerId,
        string error,
        DateTimeOffset failedAt,
        TimeSpan retryDelay,
        int maximumAttempts)
    {
        EnsureOwnedBy(workerId);

        if (maximumAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumAttempts));
        }

        LastError = error.Length <= 2000
            ? error
            : error[..2000];

        LockedBy = null;
        LeaseExpiresAt = null;

        if (AttemptCount >= maximumAttempts)
        {
            Status = AccountStatementJobStatuses.Failed;
            CompletedAt = failedAt;
            return;
        }

        Status = AccountStatementJobStatuses.Pending;
        AvailableAt = failedAt.Add(retryDelay);
    }

    private void EnsureOwnedBy(string workerId)
    {
        if (Status != AccountStatementJobStatuses.Processing ||
            LockedBy != workerId)
        {
            throw new InvalidOperationException(
                "Account statement job is not owned by this worker.");
        }
    }
}
