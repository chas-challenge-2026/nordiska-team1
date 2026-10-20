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
    private readonly IPolicyRateService _policyRateService;

    public InterestRatesController(IInterestRateService service, IPolicyRateService policyRateService)
    {
        _service = service;
        _policyRateService = policyRateService;
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

    /// <summary>
    /// Retrieves the current Riksbank policy rate.
    /// </summary>
    /// <remarks>
    /// Fetched from the Riksbank SWEA API and cached on the server for six hours. The rate is a fraction (0.0175 = 1.75 %),
    /// same as the account type rates. If the Riksbank can't be reached the last known rate is returned with stale = true.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The current policy rate and the date it took effect.</response>
    /// <response code="503">The Riksbank couldn't be reached and there is no earlier rate to fall back on.</response>
    [HttpGet("policy-rate")]
    [ProducesResponseType(typeof(PolicyRateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PolicyRateResponse>> GetPolicyRate(CancellationToken cancellationToken)
    {
        var policyRate = await _policyRateService.GetAsync(cancellationToken);
        if (policyRate is null)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service Unavailable",
                detail: "Styrräntan kunde inte hämtas från Riksbanken just nu, försök igen senare.");
        }

        return Ok(policyRate);
    }
}
