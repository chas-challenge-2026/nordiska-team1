using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nordiska.BuildingBlocks.Database;
using Nordiska.FrontendApi.Authentication.Claims;
using Nordiska.Modules.Inbox.Application;
using Nordiska.Modules.Inbox.Contracts.Requests;
using Nordiska.Modules.Inbox.Contracts.Responses;

namespace Nordiska.FrontendApi.Endpoints.Inbox;

/// <summary>
/// API endpoints for customer inbox, support tickets, and direct messaging threads.
/// </summary>
[ApiController]
[Route("api/inbox")]
[Tags("Inbox")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class InboxController(IInboxService service) : ControllerBase
{
    private readonly IInboxService _service = service;

    /// <summary>
    /// Retrieves total count of unread message threads for customer inbox badge.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Unread count response.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var result = await _service.GetUnreadCountAsync(customerId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves paginated list of conversations / support threads for the authenticated customer.
    /// </summary>
    /// <param name="folder">Folder to view ('inbox', 'sent', 'archive'). Default is 'inbox'.</param>
    /// <param name="page">Page number (1-based, default 1).</param>
    /// <param name="pageSize">Page size (1-100, default 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Paginated list of thread summaries.</response>
    /// <response code="401">Unauthorized.</response>
    [HttpGet("threads")]
    [ProducesResponseType(typeof(PagedResult<ThreadSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ThreadSummaryResponse>>> GetThreads(
        [FromQuery] string folder = "inbox",
        [FromQuery, System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int page = 1,
        [FromQuery, System.ComponentModel.DataAnnotations.Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var customerId = User.GetRequiredCustomerId();
        var query = new InboxQueryParameters(page, pageSize, folder);
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
    /// Marks all messages in the specified thread as read for the customer.
    /// </summary>
    /// <param name="id">Unique identifier of the message thread.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Thread marked as read.</response>
    /// <response code="404">Thread not found or customer has no access.</response>
    [HttpPatch("threads/{id:long}/read")]
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
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessageResponse>> StaffReply(
        [FromRoute] long id,
        [FromBody] StaffReplyRequest request,
        CancellationToken cancellationToken = default)
    {
        var staffId = User.GetCustomerId() ?? 0;
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
}
