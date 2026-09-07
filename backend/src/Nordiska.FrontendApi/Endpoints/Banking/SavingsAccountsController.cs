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
