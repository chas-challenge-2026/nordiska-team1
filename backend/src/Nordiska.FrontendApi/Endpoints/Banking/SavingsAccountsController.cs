using Microsoft.AspNetCore.Mvc;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;
namespace Nordiska.FrontendApi.Endpoints.Banking;
/// <summary>
/// API endpoints for managing savings accounts.
/// </summary>
[ApiController]
[Route("api/savingsaccounts")]
public class SavingsAccountsController : ControllerBase
{
    private readonly ISavingsAccountService _service;
    public SavingsAccountsController(ISavingsAccountService service)
    {
        _service = service;
    }
    /// <summary>
    /// Retrieves all savings accounts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of savings account responses.</returns>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SavingsAccountResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var results = await _service.GetAllAsync(cancellationToken);
        return Ok(results);
    }
    /// <summary>
    /// Retrieves a savings account by id.
    /// </summary>
    /// <param name="id">Savings account id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Savings account response.</returns>
    [HttpGet("{id}", Name = "GetSavingsAccountById")]
    public async Task<ActionResult<SavingsAccountResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(item);
    }
    /// <summary>
    /// Opens a new savings account.
    /// </summary>
    /// <param name="request">Open savings account request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created savings account.</returns>
    [HttpPost]
    public async Task<ActionResult<SavingsAccountResponse>> Create([FromBody] OpenSavingsAccountRequest request, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
