using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nordiska.FrontendApi.Authentication.Claims;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.FrontendApi.Endpoints.Banking;

/// <summary>
/// API endpoints for viewing and managing operational messages, system alerts, and header banners.
/// </summary>
[ApiController]
[Route("api/operational-messages")]
[Tags("Operational Messages")]
public class OperationalMessagesController : ControllerBase
{
    private readonly IOperationalMessageService _service;

    public OperationalMessagesController(IOperationalMessageService service)
    {
        _service = service;
    }

    /// <summary>
    /// Retrieves all currently active and unexpired operational messages for public display.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of active operational messages.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<OperationalMessageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OperationalMessageResponse>>> GetActive(CancellationToken cancellationToken)
    {
        var messages = await _service.GetActiveAsync(cancellationToken);
        return Ok(messages);
    }

    /// <summary>
    /// Retrieves all operational messages (including inactive and expired) for administrative overview.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of all operational messages.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden if user is not an administrator.</response>
    [HttpGet("all")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<OperationalMessageResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<OperationalMessageResponse>>> GetAll(CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        var messages = await _service.GetAllAsync(cancellationToken);
        return Ok(messages);
    }

    /// <summary>
    /// Retrieves a specific operational message by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Operational message details.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden if user is not an administrator.</response>
    /// <response code="404">Message was not found.</response>
    [HttpGet("{id:long}")]
    [Authorize]
    [ProducesResponseType(typeof(OperationalMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationalMessageResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        var message = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(message);
    }

    /// <summary>
    /// Creates a new bilingual operational message.
    /// </summary>
    /// <param name="request">Payload containing Swedish and English titles/messages, severity, and scheduling.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Operational message created successfully.</response>
    /// <response code="400">Invalid payload.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden if user is not an administrator.</response>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(OperationalMessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OperationalMessageResponse>> Create(
        [FromBody] CreateOperationalMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Fully updates an existing operational message.
    /// </summary>
    /// <param name="id">The unique identifier of the message to update.</param>
    /// <param name="request">Payload with updated message data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Operational message updated successfully.</response>
    /// <response code="400">Invalid payload.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden if user is not an administrator.</response>
    /// <response code="404">Message was not found.</response>
    [HttpPut("{id:long}")]
    [Authorize]
    [ProducesResponseType(typeof(OperationalMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationalMessageResponse>> Update(
        long id,
        [FromBody] UpdateOperationalMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        var updated = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Partially updates an existing operational message (e.g. toggling active state or priority).
    /// </summary>
    /// <param name="id">The unique identifier of the message.</param>
    /// <param name="request">Payload containing only the fields to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Operational message patched successfully.</response>
    /// <response code="400">Invalid payload.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden if user is not an administrator.</response>
    /// <response code="404">Message was not found.</response>
    [HttpPatch("{id:long}")]
    [Authorize]
    [ProducesResponseType(typeof(OperationalMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationalMessageResponse>> Patch(
        long id,
        [FromBody] PatchOperationalMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        var patched = await _service.PatchAsync(id, request, cancellationToken);
        return Ok(patched);
    }

    /// <summary>
    /// Deletes an operational message by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the message to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Message deleted successfully.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden if user is not an administrator.</response>
    /// <response code="404">Message was not found.</response>
    [HttpDelete("{id:long}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
