using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.FrontendApi.Endpoints.Banking;

/// <summary>
/// API endpoints for reading interest rates per account type.
/// </summary>
[ApiController]
[Route("api/interest-rates")]
[Tags("Interest rates")]
[AllowAnonymous]
public sealed class InterestRatesController : ControllerBase
{
    private readonly IInterestRateService _service;

    public InterestRatesController(IInterestRateService service)
    {
        _service = service;
    }

    /// <summary>
    /// Retrieves the current interest rate for every account type.
    /// </summary>
    /// <remarks>
    /// Interest rates are public information and are cached on the server for one hour.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of interest rates per account type.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AccountTypeConfigResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AccountTypeConfigResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var rates = await _service.GetAllAsync(cancellationToken);
        return Ok(rates);
    }
}
