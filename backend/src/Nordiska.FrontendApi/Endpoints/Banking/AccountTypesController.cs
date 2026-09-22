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
/// API endpoints for retrieving and configuring bank account types and interest rates.
/// </summary>
[ApiController]
[Route("api/account-types")]
[Route("api/banking/account-types")]
[Tags("Account Types")]
public class AccountTypesController : ControllerBase
{
    private readonly IAccountTypeConfigService _service;

    public AccountTypesController(IAccountTypeConfigService service)
    {
        _service = service;
    }

    /// <summary>
    /// Retrieves all available account types, their current interest rates, and descriptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of account type configurations.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<AccountTypeConfigResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AccountTypeConfigResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _service.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves details and current interest rate for a specific account type.
    /// </summary>
    /// <param name="type">The account type key (e.g. 'flex', 'fix', 'standard', 'saving', 'premium').</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Account type details.</response>
    /// <response code="404">Account type not found.</response>
    [HttpGet("{type}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AccountTypeConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountTypeConfigResponse>> GetByType(string type, CancellationToken cancellationToken)
    {
        var result = await _service.GetByTypeAsync(type, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new account type configuration with a specified interest rate.
    /// </summary>
    /// <param name="request">Payload containing account type key, interest rate, and description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">Account type created successfully.</response>
    /// <response code="400">Invalid request payload.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden if user is not an administrator.</response>
    /// <response code="409">Account type already exists.</response>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(AccountTypeConfigResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountTypeConfigResponse>> Create([FromBody] CreateAccountTypeConfigRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByType), new { type = created.AccountType }, created);
    }

    /// <summary>
    /// Updates the interest rate or description of an existing account type.
    /// </summary>
    /// <param name="type">The account type key (e.g. 'flex', 'fix', 'standard', 'saving', 'premium').</param>
    /// <param name="request">Payload containing new interest rate and/or description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Account type updated successfully.</response>
    /// <response code="400">Invalid request payload.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden if user is not an administrator.</response>
    /// <response code="404">Account type not found.</response>
    [HttpPut("{type}")]
    [Authorize]
    [ProducesResponseType(typeof(AccountTypeConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountTypeConfigResponse>> Update(string type, [FromBody] UpdateAccountTypeConfigRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        var updated = await _service.UpdateAsync(type, request, cancellationToken);
        return Ok(updated);
    }
}
