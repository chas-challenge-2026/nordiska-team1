using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nordiska.FrontendApi.Contracts.Mappers;
using Nordiska.FrontendApi.Contracts.Requests;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;

namespace Nordiska.FrontendApi.Endpoints.Banking;

/// <summary>
/// API endpoints for managing customer profiles.
/// </summary>
[ApiController]
[Route("api/customers")]
[Authorize]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;

    public CustomersController(ICustomerService service)
    {
        _service = service;
    }

    /// <summary>
    /// Retrieves a customer profile by their unique identifier.
    /// </summary>
    /// <param name="id">The customer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Customer profile retrieved successfully.</response>
    /// <response code="403">Forbidden if the user is not authorized to access this profile.</response>
    /// <response code="404">Customer with the given ID was not found.</response>
    [HttpGet("{id}", Name = "GetCustomerById")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        if (!IsAuthorizedForCustomer(id))
        {
            return Forbid();
        }

        var customer = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(customer.ToResponse());
    }

    /// <summary>
    /// Creates a new customer profile.
    /// </summary>
    /// <remarks>
    /// Allows creating a customer record with full name, email, Swedish personal number, and optional phone number.
    /// </remarks>
    /// <param name="request">Customer details to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Customer created successfully.</response>
    /// <response code="400">Invalid customer creation payload.</response>
    [AllowAnonymous]
    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CustomerResponse>> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request.Name, request.Email, request.PersonalNum, request.PhoneNumber, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToResponse());
    }

    /// <summary>
    /// Updates an existing customer profile.
    /// </summary>
    /// <param name="request">Updated customer payload including ID, name, email, personal number, and phone number.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="id">Optional customer ID from route.</param>
    /// <response code="200">Customer updated successfully.</response>
    /// <response code="400">Validation failed on the updated customer data.</response>
    /// <response code="403">Forbidden if the user is not authorized to update this profile.</response>
    /// <response code="404">Customer not found.</response>
    [HttpPut]
    [HttpPut("{id}")]
    [HttpPut("{id}/profile")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> Update([FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken, [FromRoute] long? id = null)
    {
        var targetId = id ?? request.Id;
        if (targetId == 0)
        {
            return BadRequest(new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = "Customer ID is required." });
        }

        if (!IsAuthorizedForCustomer(targetId))
        {
            return Forbid();
        }

        var updated = await _service.UpdateAsync(targetId, request.Name, request.Email, request.PersonalNum, request.EffectivePhone, cancellationToken);
        return Ok(updated.ToResponse());
    }

    /// <summary>
    /// Partially updates specific customer profile fields (e.g. email or phone number).
    /// </summary>
    /// <param name="id">Customer ID from route.</param>
    /// <param name="request">Partial customer fields to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Customer partially updated successfully.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Forbidden if accessing another customer's profile.</response>
    /// <response code="404">Customer not found.</response>
    [HttpPatch("{id}")]
    [HttpPatch("{id}/profile")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> Patch(long id, [FromBody] PatchCustomerRequest request, CancellationToken cancellationToken)
    {
        if (!IsAuthorizedForCustomer(id))
        {
            return Forbid();
        }

        var updated = await _service.PatchProfileAsync(id, request.Name, request.Email, request.EffectivePhone, cancellationToken);
        return Ok(updated.ToResponse());
    }

    /// <summary>
    /// Partially updates specific customer profile fields using request body ID.
    /// </summary>
    [HttpPatch]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> PatchBody([FromBody] PatchCustomerRequest request, CancellationToken cancellationToken)
    {
        var targetId = request.Id ?? GetCurrentUserId();
        if (targetId == 0 || !IsAuthorizedForCustomer(targetId))
        {
            return Forbid();
        }

        var updated = await _service.PatchProfileAsync(targetId, request.Name, request.Email, request.EffectivePhone, cancellationToken);
        return Ok(updated.ToResponse());
    }

    /// <summary>
    /// Deletes a customer profile.
    /// </summary>
    /// <param name="id">The customer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Customer deleted successfully.</response>
    /// <response code="400">Cannot delete customer with positive account balance.</response>
    /// <response code="403">Forbidden if the user is not authorized to delete this profile.</response>
    /// <response code="404">Customer not found.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        if (!IsAuthorizedForCustomer(id))
        {
            return Forbid();
        }

        await _service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private bool IsAuthorizedForCustomer(long customerId)
    {
        if (User.IsInRole("Admin"))
        {
            return true;
        }

        var currentUserId = GetCurrentUserId();
        return currentUserId != 0 && currentUserId == customerId;
    }

    private long GetCurrentUserId()
    {
        var currentUserIdStr = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                               ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return long.TryParse(currentUserIdStr, out var currentUserId) ? currentUserId : 0;
    }
}
