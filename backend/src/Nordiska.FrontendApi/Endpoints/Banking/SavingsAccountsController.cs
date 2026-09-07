using Microsoft.AspNetCore.Mvc;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.FrontendApi.Endpoints.Banking;

[ApiController]
[Route("api/savingsaccounts")]
public class SavingsAccountsController : ControllerBase
{
    private readonly ISavingsAccountService _service;

    // Dependency injection of the ISavingsAccountService
    // This controller handles HTTP requests related to savings accounts.
    // It has get, get by id, and post endpoints for querying and creating savings accounts.

    // TODO: Add authentication and authorization to ensure that only authorized users can access these endpoints.
    // TODO: Add pagination and filtering to the GetAll endpoint.
    public SavingsAccountsController(ISavingsAccountService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SavingsAccountResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var results = await _service.GetAllAsync(cancellationToken);
        return Ok(results);
    }

    [HttpGet("{id}", Name = "GetSavingsAccountById")]
    public async Task<ActionResult<SavingsAccountResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<SavingsAccountResponse>> Create([FromBody] OpenSavingsAccountRequest request, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
