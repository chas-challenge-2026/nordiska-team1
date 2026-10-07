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
/// API endpoints for managing and automating savings goals.
/// </summary>
[ApiController]
[Route("api/savings-goals")]
[Authorize]
public class SavingsGoalsController : ControllerBase
{
    private readonly ISavingsGoalService _savingsGoalService;

    public SavingsGoalsController(ISavingsGoalService savingsGoalService)
    {
        _savingsGoalService = savingsGoalService;
    }

    /// <summary>
    /// Retrieves savings goals for the authenticated customer, optionally filtered by account.
    /// </summary>
    /// <param name="accountId">Optional savings account identifier to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of savings goals.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<SavingsGoalResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<SavingsGoalResponse>>> GetGoals(
        [FromQuery] long? accountId,
        CancellationToken cancellationToken)
    {
        var customerId = User.GetRequiredCustomerId();
        var isAdmin = User.IsAdmin();

        var goals = await _savingsGoalService.GetGoalsAsync(
            customerId,
            accountId,
            isAdmin,
            cancellationToken);

        return Ok(goals);
    }

    /// <summary>
    /// Retrieves a specific savings goal by its identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the savings goal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The savings goal details.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">The savings goal was not found.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SavingsGoalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavingsGoalResponse>> GetById(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var customerId = User.GetRequiredCustomerId();
        var isAdmin = User.IsAdmin();

        var goal = await _savingsGoalService.GetByIdAsync(
            id,
            customerId,
            isAdmin,
            cancellationToken);

        if (goal is null)
        {
            return NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Sparmål hittades inte." });
        }

        return Ok(goal);
    }

    /// <summary>
    /// Creates a new savings goal attached to a customer's savings account.
    /// </summary>
    /// <param name="request">Goal payload including account identifier, title, target amount, and optional target date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">The savings goal was successfully created.</response>
    /// <response code="400">Invalid parameters provided.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">The linked savings account was not found.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SavingsGoalResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavingsGoalResponse>> Create(
        [FromBody] CreateSavingsGoalRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = User.GetRequiredCustomerId();
        var isAdmin = User.IsAdmin();

        var created = await _savingsGoalService.CreateAsync(
            request,
            customerId,
            isAdmin,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates details of an existing savings goal (title, target amount, target date, or status).
    /// </summary>
    /// <param name="id">The unique identifier of the savings goal.</param>
    /// <param name="request">Fields to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The savings goal was successfully updated.</response>
    /// <response code="400">Invalid parameters provided.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">The savings goal was not found.</response>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(SavingsGoalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavingsGoalResponse>> Update(
        [FromRoute] long id,
        [FromBody] UpdateSavingsGoalRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = User.GetRequiredCustomerId();
        var isAdmin = User.IsAdmin();

        var updated = await _savingsGoalService.UpdateAsync(
            id,
            request,
            customerId,
            isAdmin,
            cancellationToken);

        return Ok(updated);
    }

    /// <summary>
    /// Deletes a savings goal and cancels any associated automated recurring transfer.
    /// </summary>
    /// <param name="id">The unique identifier of the savings goal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The savings goal was successfully deleted.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">The savings goal was not found.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var customerId = User.GetRequiredCustomerId();
        var isAdmin = User.IsAdmin();

        await _savingsGoalService.DeleteAsync(
            id,
            customerId,
            isAdmin,
            cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Schedules or updates an automated recurring monthly transfer towards a savings goal.
    /// </summary>
    /// <param name="id">The unique identifier of the savings goal.</param>
    /// <param name="request">Automation settings including source account, monthly amount, and day of month.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The monthly automation was successfully scheduled.</response>
    /// <response code="400">Invalid parameters provided.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">The savings goal or source account was not found.</response>
    [HttpPost("{id}/automate")]
    [ProducesResponseType(typeof(AutomateSavingsGoalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AutomateSavingsGoalResponse>> Automate(
        [FromRoute] long id,
        [FromBody] AutomateSavingsGoalRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = User.GetRequiredCustomerId();
        var isAdmin = User.IsAdmin();

        var result = await _savingsGoalService.AutomateAsync(
            id,
            request,
            customerId,
            isAdmin,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Cancels an automated recurring monthly savings transfer for a savings goal.
    /// </summary>
    /// <param name="id">The unique identifier of the savings goal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">The automated savings transfer was successfully cancelled.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="404">The savings goal was not found.</response>
    [HttpDelete("{id}/automate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAutomate(
        [FromRoute] long id,
        CancellationToken cancellationToken)
    {
        var customerId = User.GetRequiredCustomerId();
        var isAdmin = User.IsAdmin();

        await _savingsGoalService.CancelAutomationAsync(
            id,
            customerId,
            isAdmin,
            cancellationToken);

        return NoContent();
    }
}
