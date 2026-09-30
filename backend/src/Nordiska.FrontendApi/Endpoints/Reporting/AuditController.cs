using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nordiska.Modules.Reporting.Application;
using Nordiska.Modules.Reporting.Domain;

namespace Nordiska.FrontendApi.Endpoints.Reporting;

/// <summary>
/// API endpoints for viewing and cryptographically verifying immutable audit logs.
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
    }

    /// <summary>
    /// Retrieves paginated audit log entries with optional filtering.
    /// Non-admin users can only view entries belonging to their own user ID.
    /// </summary>
    /// <param name="action">Optional filter for action type (e.g. AUTH_LOGIN, TRANSACTION_DEPOSIT).</param>
    /// <param name="userId">Optional filter for target user ID (admin only).</param>
    /// <param name="fromDate">Optional start date for filtering.</param>
    /// <param name="toDate">Optional end date for filtering.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Page size (default: 50, max: 200).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns list of audit entries and total count.</response>
    /// <response code="401">Unauthorized if authentication token is missing.</response>
    [HttpGet]
    [ProducesResponseType(typeof(AuditEntriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuditEntriesResponse>> GetAuditEntries(
        [FromQuery] string? action,
        [FromQuery] long? userId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var effectiveUserId = IsAdmin() ? userId : currentUserId;

        var (entries, totalCount) = await _auditLogService.GetEntriesAsync(
            effectiveUserId,
            action,
            fromDate,
            toDate,
            page,
            pageSize,
            cancellationToken);

        var dtos = entries.Select(e => new AuditEntryDto(
            e.Id,
            e.Action,
            e.UserId,
            e.Details,
            e.Signature,
            e.CreatedAt,
            _auditLogService.VerifySignature(e)
        )).ToList();

        return Ok(new AuditEntriesResponse(dtos, totalCount, page, pageSize));
    }

    /// <summary>
    /// Cryptographically verifies the HMAC-SHA256 signature of a specific audit log entry.
    /// </summary>
    /// <param name="id">The audit entry identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the cryptographic verification result.</response>
    /// <response code="404">Audit entry with the specified ID was not found.</response>
    /// <response code="403">Forbidden if accessing an entry belonging to another customer.</response>
    [HttpGet("{id:long}/verify")]
    [ProducesResponseType(typeof(AuditVerificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuditVerificationResponse>> VerifyAuditEntry(
        long id,
        CancellationToken cancellationToken)
    {
        var entry = await _auditLogService.GetByIdAsync(id, cancellationToken);
        if (entry is null)
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Audit entry not found." });
        }

        var currentUserId = GetCurrentUserId();
        if (!IsAdmin() && entry.UserId.HasValue && entry.UserId.Value != currentUserId)
        {
            return Forbid();
        }

        var isValid = _auditLogService.VerifySignature(entry);

        return Ok(new AuditVerificationResponse(
            entry.Id,
            entry.Action,
            entry.UserId,
            isValid,
            entry.Signature,
            entry.CreatedAt
        ));
    }

    private bool IsAdmin() => User.IsInRole("Admin");

    private long GetCurrentUserId()
    {
        var currentUserIdStr = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                               ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return long.TryParse(currentUserIdStr, out var currentUserId) ? currentUserId : 0;
    }
}

public sealed record AuditEntryDto(
    long Id,
    string Action,
    long? UserId,
    string Details,
    string Signature,
    DateTime CreatedAt,
    bool IsSignatureValid);

public sealed record AuditEntriesResponse(
    IReadOnlyList<AuditEntryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AuditVerificationResponse(
    long Id,
    string Action,
    long? UserId,
    bool IsValid,
    string Signature,
    DateTime CreatedAt);
