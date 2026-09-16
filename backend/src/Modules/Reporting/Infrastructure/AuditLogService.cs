using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;
using Nordiska.Modules.Reporting.Infrastructure.Db;

namespace Nordiska.Modules.Reporting.Infrastructure;

public sealed class AuditLogService : IAuditLogService
{
    private const string DefaultSigningKey = "Nordiska-Default-Audit-Signing-Key-2026-SuperSecure";
    private readonly ReportingDbContext _dbContext;
    private readonly byte[] _signingKeyBytes;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        ReportingDbContext dbContext,
        IConfiguration configuration,
        ILogger<AuditLogService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var signingKey = configuration["AuditLogging:SigningKey"] ?? DefaultSigningKey;
        _signingKeyBytes = Encoding.UTF8.GetBytes(signingKey);
    }

    public async Task<AuditEntry> LogAsync(
        string action,
        long? userId,
        string details,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        details ??= string.Empty;

        // PostgreSQL stores timestamps with microsecond precision (6 decimal places).
        // Normalize ticks to microsecond precision so signature matches identically in memory and after DB persistence.
        var ticks = (DateTime.UtcNow.Ticks / 10) * 10;
        var createdAt = new DateTime(ticks, DateTimeKind.Utc);
        var signature = ComputeSignature(action, userId, createdAt, details);

        var entry = new AuditEntry
        {
            Action = action,
            UserId = userId,
            Details = details,
            Signature = signature,
            CreatedAt = createdAt
        };

        _dbContext.AuditEntries.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Recorded audit entry {Action} for user {UserId} with id {AuditId}",
            action, userId, entry.Id);

        return entry;
    }

    public bool VerifySignature(AuditEntry entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.Signature))
        {
            return false;
        }

        var expectedSignature = ComputeSignature(entry.Action, entry.UserId, entry.CreatedAt, entry.Details);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        var actualBytes = Encoding.UTF8.GetBytes(entry.Signature.ToLowerInvariant());

        if (expectedBytes.Length != actualBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    public async Task<(IReadOnlyList<AuditEntry> Entries, int TotalCount)> GetEntriesAsync(
        long? userId = null,
        string? action = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 200) pageSize = 200;

        var query = _dbContext.AuditEntries.AsNoTracking().AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(x => x.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(x => x.Action == action);
        }

        if (fromDate.HasValue)
        {
            var fromUtc = fromDate.Value.ToUniversalTime();
            query = query.Where(x => x.CreatedAt >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toUtc = toDate.Value.ToUniversalTime();
            query = query.Where(x => x.CreatedAt <= toUtc);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var entries = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (entries, totalCount);
    }

    public async Task<AuditEntry?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.AuditEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private string ComputeSignature(string action, long? userId, DateTime createdAtUtc, string details)
    {
        var rawPayload = $"{action}|{userId?.ToString() ?? "null"}|{FormatTimestamp(createdAtUtc)}|{details}";
        var payloadBytes = Encoding.UTF8.GetBytes(rawPayload);
        var hashBytes = HMACSHA256.HashData(_signingKeyBytes, payloadBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string FormatTimestamp(DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        return utc.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'ffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);
    }
}
