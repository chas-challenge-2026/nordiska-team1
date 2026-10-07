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
