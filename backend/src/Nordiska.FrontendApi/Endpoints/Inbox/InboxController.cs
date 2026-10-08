using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nordiska.BuildingBlocks.Database;
using Nordiska.FrontendApi.Authentication.Claims;
using Nordiska.FrontendApi.Filters;
using Nordiska.Modules.Inbox.Application;
using Nordiska.Modules.Inbox.Contracts.Requests;
using Nordiska.Modules.Inbox.Contracts.Responses;

namespace Nordiska.FrontendApi.Endpoints.Inbox;

/// <summary>
/// API endpoints for customer inbox, support tickets, and direct messaging threads.
/// </summary>
[ApiController]
[Route("api/inbox")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class InboxController(IInboxService service) : ControllerBase
{
    private readonly IInboxService _service = service;

    /// <summary>
    /// Retrieves total count of unread items across all categories (threads, notifications, unopened documents, pending terms) for customer inbox badges.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Unread count response including total unread and category breakdowns.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpGet("unread-count")]
    [Tags("Inbox - Overview & Feed")]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var result = await _service.GetUnreadCountAsync(customerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a unified dashboard overview designed for the primary customer Inbox UI view.
    /// Returns unread summary counts, a chronological unified feed of recent events, dedicated unread feed, and urgent pending terms.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Unified inbox overview response.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpGet("overview")]
    [Tags("Inbox - Overview & Feed")]
    [ProducesResponseType(typeof(InboxOverviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InboxOverviewResponse>> GetOverview(CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var result = await _service.GetOverviewAsync(customerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves a paginated and filterable unified timeline feed of customer inbox events.
    /// Aggregates support threads, system notifications, archived documents, and terms.
    /// </summary>
    /// <param name="type">Optional filter by event type ('thread', 'notification', 'document', 'term').</param>
    /// <param name="unreadOnly">Filter to only unread, unopened, or action-required items.</param>
    /// <param name="page">Page number (1-based, default 1).</param>
    /// <param name="pageSize">Page size (1-100, default 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Paginated list of feed items.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpGet("feed")]
    [Tags("Inbox - Overview & Feed")]
    [ProducesResponseType(typeof(PagedResult<InboxFeedItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<InboxFeedItemResponse>>> GetFeed(
        [FromQuery] string? type = null,
        [FromQuery] bool unreadOnly = false,
        [FromQuery, System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int page = 1,
        [FromQuery, System.ComponentModel.DataAnnotations.Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var query = new FeedQueryParameters(type, unreadOnly, page, pageSize);
        var result = await _service.GetFeedAsync(customerId, query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Marks all customer message threads and all notifications as read in a single operation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Result confirming how many items were marked as read.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpPost("read-all")]
    [HttpPatch("read-all")]
    [Tags("Inbox - Overview & Feed")]
    [ProducesResponseType(typeof(MarkAllReadResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MarkAllReadResponse>> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var result = await _service.MarkAllAsReadAsync(customerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves paginated list of conversations / support threads for the authenticated customer.
    /// </summary>
    /// <param name="folder">Folder to view ('inbox', 'sent', 'archive'). Default is 'inbox'.</param>
    /// <param name="searchTerm">Optional search query to filter by subject or message content.</param>
    /// <param name="page">Page number (1-based, default 1).</param>
    /// <param name="pageSize">Page size (1-100, default 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Paginated list of thread summaries.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpGet("threads")]
    [Tags("Inbox - Messages & Support")]
    [ProducesResponseType(typeof(PagedResult<ThreadSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ThreadSummaryResponse>>> GetThreads(
        [FromQuery] string folder = "inbox",
        [FromQuery] string? searchTerm = null,
        [FromQuery, System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int page = 1,
        [FromQuery, System.ComponentModel.DataAnnotations.Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var query = new InboxQueryParameters(page, pageSize, folder, searchTerm);
        var result = await _service.GetThreadsAsync(customerId, query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves full details and message history for a specific thread.
    /// </summary>
    /// <param name="id">Unique identifier of the message thread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Thread details and messages.</response>
    /// <response code="404">Thread not found or belongs to another customer.</response>
    [HttpGet("threads/{id:long}")]
    [Tags("Inbox - Messages & Support")]
    [ProducesResponseType(typeof(ThreadDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ThreadDetailResponse>> GetThread(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var thread = await _service.GetThreadDetailsAsync(customerId, id, cancellationToken);

        if (thread is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Hittades inte",
                Detail = $"Ärendet med id '{id}' kunde inte hittas.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(thread);
    }

    /// <summary>
    /// Creates a new support ticket / inquiry thread with an initial message.
    /// </summary>
    /// <param name="request">Ticket subject, category (Sparkonto, Sparmål, Skatteunderlag, Allmänt), and message body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Thread created successfully.</response>
    /// <response code="400">Validation error.</response>
    [HttpPost("threads")]
    [Tags("Inbox - Messages & Support")]
    [ProducesResponseType(typeof(ThreadDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ThreadDetailResponse>> CreateThread(
        [FromBody] CreateThreadRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        try
        {
            var created = await _service.CreateSupportTicketAsync(customerId, request, cancellationToken);
            return CreatedAtAction(nameof(GetThread), new { id = created.Id }, created);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Valideringsfel",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// Sends a reply message to an existing support thread.
    /// </summary>
    /// <param name="id">Unique identifier of the message thread.</param>
    /// <param name="request">Reply message content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Message sent successfully.</response>
    /// <response code="400">Validation error or thread is closed/reply not allowed.</response>
    /// <response code="404">Thread not found or customer has no access.</response>
    [HttpPost("threads/{id:long}/messages")]
    [Tags("Inbox - Messages & Support")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageResponse>> ReplyToThread(
        [FromRoute] long id,
        [FromBody] ReplyThreadRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        try
        {
            var message = await _service.ReplyToThreadAsync(customerId, id, request, cancellationToken);
            if (message is null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Hittades inte",
                    Detail = $"Ärendet med id '{id}' kunde inte hittas.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return StatusCode(StatusCodes.Status201Created, message);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Valideringsfel",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Otillåten åtgärd",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// Marks a single specific support thread as read for the customer.
    /// Note: To mark all threads and notifications across the entire inbox as read in one operation, use POST /api/inbox/read-all.
    /// </summary>
    /// <param name="id">Unique identifier of the message thread to mark as read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Thread marked as read.</response>
    /// <response code="404">Thread not found or customer has no access.</response>
    [HttpPatch("threads/{id:long}/read")]
    [HttpPost("threads/{id:long}/read")]
    [Tags("Inbox - Messages & Support")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var success = await _service.MarkAsReadAsync(customerId, id, cancellationToken);
        if (!success)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Hittades inte",
                Detail = $"Ärendet med id '{id}' kunde inte hittas.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Moves the specified thread to the archive folder for the customer.
    /// </summary>
    /// <param name="id">Unique identifier of the message thread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Thread archived.</response>
    /// <response code="404">Thread not found or customer has no access.</response>
    [HttpPatch("threads/{id:long}/archive")]
    [Tags("Inbox - Messages & Support")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveThread(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var success = await _service.ArchiveThreadAsync(customerId, id, cancellationToken);
        if (!success)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Hittades inte",
                Detail = $"Ärendet med id '{id}' kunde inte hittas.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Restores an archived message thread back to the inbox folder for the customer.
    /// </summary>
    /// <param name="id">Unique identifier of the message thread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Thread restored to inbox.</response>
    /// <response code="404">Thread not found or customer has no access.</response>
    [HttpPatch("threads/{id:long}/restore")]
    [HttpPatch("threads/{id:long}/unarchive")]
    [Tags("Inbox - Messages & Support")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreThread(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var success = await _service.RestoreThreadAsync(customerId, id, cancellationToken);
        if (!success)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Hittades inte",
                Detail = $"Ärendet med id '{id}' kunde inte hittas.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Closes the specified support thread, marking it as resolved and blocking further replies.
    /// </summary>
    /// <param name="id">Unique identifier of the message thread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Thread closed successfully.</response>
    /// <response code="404">Thread not found or customer has no access.</response>
    [HttpPatch("threads/{id:long}/close")]
    [Tags("Inbox - Messages & Support")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloseThread(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        var isStaff = User.IsInRole("Admin") || User.IsInRole("Staff");
        var customerId = isStaff ? 0 : User.GetRequiredCustomerId();
        var success = await _service.CloseThreadAsync(customerId, id, isStaff, cancellationToken);
        if (!success)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Hittades inte",
                Detail = $"Ärendet med id '{id}' kunde inte hittas.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Reopens a closed support thread allowing replies again.
    /// </summary>
    /// <param name="id">Unique identifier of the message thread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Thread reopened successfully.</response>
    /// <response code="404">Thread not found or customer has no access.</response>
    [HttpPatch("threads/{id:long}/reopen")]
    [Tags("Inbox - Messages & Support")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReopenThread(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        var isStaff = User.IsInRole("Admin") || User.IsInRole("Staff");
        var customerId = isStaff ? 0 : User.GetRequiredCustomerId();
        var success = await _service.ReopenThreadAsync(customerId, id, isStaff, cancellationToken);
        if (!success)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Hittades inte",
                Detail = $"Ärendet med id '{id}' kunde inte hittas.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Retrieves a paginated list of support threads across all customers for bank staff/admin.
    /// </summary>
    /// <param name="parameters">Filter options including customerId, status, and searchTerm.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of admin thread summaries.</response>
    /// <response code="403">Forbidden if not bank staff or admin.</response>
    [HttpGet("admin/threads")]
    [Authorize(Roles = "Admin,Staff")]
    [Tags("Inbox - Admin & Staff")]
    [ProducesResponseType(typeof(PagedResult<AdminThreadSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AdminThreadSummaryResponse>>> GetAdminThreads(
        [FromQuery] AdminThreadQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetAdminThreadsAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves full details and message history for any support thread across all customers for bank staff/admin.
    /// </summary>
    /// <param name="id">Unique identifier of the message thread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Thread details and complete message history.</response>
    /// <response code="403">Forbidden if not bank staff or admin.</response>
    /// <response code="404">Thread not found.</response>
    [HttpGet("admin/threads/{id:long}")]
    [Authorize(Roles = "Admin,Staff")]
    [Tags("Inbox - Admin & Staff")]
    [ProducesResponseType(typeof(ThreadDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ThreadDetailResponse>> GetAdminThread(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        var thread = await _service.GetAdminThreadDetailsAsync(id, cancellationToken);
        if (thread is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Hittades inte",
                Detail = $"Ärendet med id '{id}' kunde inte hittas.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(thread);
    }

    /// <summary>
    /// Creates a direct support inquiry or general broadcast information message from bank staff/admin.
    /// </summary>
    /// <param name="request">Admin message options including customerId or broadcastToAll.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Message(s) created successfully.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="403">Forbidden if not bank staff or admin.</response>
    [HttpPost("admin/threads")]
    [Authorize(Roles = "Admin,Staff")]
    [Tags("Inbox - Admin & Staff")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAdminThread(
        [FromBody] CreateAdminThreadRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _service.CreateAdminThreadAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new
            {
                Success = true,
                CreatedCount = count,
                Message = request.BroadcastToAll
                    ? $"Meddelande skickades till {count} kunder."
                    : "Meddelande skapades för kunden."
            });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Valideringsfel",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// Retrieves a paginated list of notifications for the authenticated customer.
    /// </summary>
    /// <param name="unreadOnly">Filter only unread notifications.</param>
    /// <param name="page">Page number (1-based, default 1).</param>
    /// <param name="pageSize">Page size (1-100, default 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Paginated list of customer notifications.</response>
    [HttpGet("notifications")]
    [Tags("Inbox - Notifications")]
    [ProducesResponseType(typeof(PagedResult<CustomerNotificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomerNotificationResponse>>> GetNotifications(
        [FromQuery] bool unreadOnly = false,
        [FromQuery, System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int page = 1,
        [FromQuery, System.ComponentModel.DataAnnotations.Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var query = new NotificationQueryParameters(unreadOnly, page, pageSize);
        var result = await _service.GetNotificationsAsync(customerId, query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Marks a specific customer notification as read.
    /// </summary>
    /// <param name="id">Unique identifier of the notification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Notification marked as read.</response>
    /// <response code="404">Notification not found.</response>
    [HttpPatch("notifications/{id:long}/read")]
    [Tags("Inbox - Notifications")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkNotificationRead(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var success = await _service.MarkNotificationReadAsync(customerId, id, cancellationToken);
        if (!success)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Hittades inte",
                Detail = $"Notisen med id '{id}' kunde inte hittas.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return NoContent();
    }

    /// <summary>
    /// Marks all unread notifications as read for the authenticated customer.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Total count of notifications marked as read.</response>
    [HttpPatch("notifications/read-all")]
    [Tags("Inbox - Notifications")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var count = await _service.MarkAllNotificationsReadAsync(customerId, cancellationToken);
        return Ok(new { Count = count, Message = $"{count} notiser markerades som lästa." });
    }

    /// <summary>
    /// Allows bank staff/admin to post a reply to a customer support thread, logging sender_staff_id and notifying customer.
    /// </summary>
    /// <param name="id">Unique identifier of the message thread.</param>
    /// <param name="request">Staff response content and reply permissions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Staff reply posted and customer notified.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="403">Forbidden if not bank staff/admin.</response>
    /// <response code="404">Thread not found.</response>
    [HttpPost("threads/{id:long}/staff-reply")]
    [Authorize(Roles = "Admin")]
    [Tags("Inbox - Admin & Staff")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageResponse>> StaffReply(
        [FromRoute] long id,
        [FromBody] StaffReplyRequest request,
        CancellationToken cancellationToken = default)
    {
        var staffId = User.GetRequiredStaffId();
        try
        {
            var message = await _service.AddStaffReplyAsync(staffId, id, request, cancellationToken);
            if (message is null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Hittades inte",
                    Detail = $"Ärendet med id '{id}' kunde inte hittas.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return StatusCode(StatusCodes.Status201Created, message);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Valideringsfel",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// Retrieves a paginated list of archived documents for the authenticated customer.
    /// Supports filtering by year and document type (e.g. TaxReport, AnnualStatement, Agreement).
    /// </summary>
    /// <param name="parameters">Filter and pagination options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of archived documents.</response>
    /// <response code="401">Unauthorized if not authenticated.</response>
    [HttpGet("documents")]
    [Tags("Inbox - Documents")]
    [ProducesResponseType(typeof(PagedResult<DocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<DocumentResponse>>> GetDocuments(
        [FromQuery] DocumentQueryParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var documents = await _service.GetDocumentsAsync(customerId, parameters, cancellationToken);
        return Ok(documents);
    }

    /// <summary>
    /// Retrieves bank-wide general legal documents, standard agreements, and terms without customer personal data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of bank general documents and terms.</response>
    /// <response code="401">Unauthorized if not authenticated.</response>
    [HttpGet("documents/general")]
    [Tags("Inbox - Documents")]
    [ProducesResponseType(typeof(IReadOnlyList<GeneralDocumentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<GeneralDocumentResponse>>> GetGeneralDocuments(
        CancellationToken cancellationToken = default)
    {
        var documents = await _service.GetGeneralDocumentsAsync(cancellationToken);
        return Ok(documents);
    }

    /// <summary>
    /// Downloads an archived document securely by its ID, verifying checksum integrity and logging first open timestamp for legal compliance.
    /// </summary>
    /// <param name="id">Unique identifier of the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The PDF file stream.</response>
    /// <response code="401">Unauthorized if not authenticated.</response>
    /// <response code="404">Document not found.</response>
    [HttpGet("documents/{id:long}/download")]
    [Tags("Inbox - Documents")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocument(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var result = await _service.DownloadDocumentAsync(customerId, id, cancellationToken);
        if (result is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Dokument hittades inte",
                Detail = $"Dokumentet med id '{id}' kunde inte hittas.",
                Status = StatusCodes.Status404NotFound
            });
        }

        Response.Headers.Append("X-Document-Checksum-Sha256", result.Sha256);
        Response.Headers.Append("X-Document-Checksum-Valid", result.IsChecksumValid.ToString().ToLowerInvariant());

        return File(result.Content, result.MimeType, result.FileName);
    }

    /// <summary>
    /// Retrieves all pending terms and condition updates requiring digital acceptance from the customer
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of pending terms awaiting acceptance.</response>
    /// <response code="401">Unauthorized if not authenticated.</response>
    [HttpGet("terms/pending")]
    [Tags("Inbox - Terms & Agreements")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingTermResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PendingTermResponse>>> GetPendingTerms(
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var pendingTerms = await _service.GetPendingTermsAsync(customerId, cancellationToken);
        return Ok(pendingTerms);
    }

    /// <summary>
    /// Digitally accepts an updated term or condition, recording audit log and timestamp
    /// </summary>
    /// <param name="id">Unique identifier of the term to accept.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Term acceptance confirmed.</response>
    /// <response code="401">Unauthorized if not authenticated.</response>
    /// <response code="404">Term not found or not pending for this customer.</response>
    [HttpPost("terms/{id:long}/accept")]
    [AuditAction("TERMS_ACCEPT")]
    [Tags("Inbox - Terms & Agreements")]
    [ProducesResponseType(typeof(TermAcceptanceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TermAcceptanceResult>> AcceptTerm(
        [FromRoute] long id,
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var result = await _service.AcceptTermAsync(customerId, id, cancellationToken);
        if (!result.Success)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Villkor hittades inte",
                Detail = result.Message,
                Status = StatusCodes.Status404NotFound
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Publishes a new version of general terms or policy, generating pending acceptance records for affected customers
    /// </summary>
    /// <param name="request">Payload containing code, version, title, and document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Term published successfully.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden if not staff or admin.</response>
    [HttpPost("terms/publish")]
    [Authorize(Roles = "Admin,Staff")]
    [AuditAction("TERMS_PUBLISH")]
    [Tags("Inbox - Terms & Agreements")]
    [ProducesResponseType(typeof(TermResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TermResponse>> PublishTerm(
        [FromBody] PublishTermRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _service.PublishTermAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}