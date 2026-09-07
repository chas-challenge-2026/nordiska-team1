using Microsoft.AspNetCore.Mvc;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.Modules.Banking.Contracts.Responses;

namespace Nordiska.FrontendApi.Endpoints.Banking;

[ApiController]
[Route("api/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _service;

    public TransactionsController(ITransactionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetAll([FromQuery] long? accountId, CancellationToken cancellationToken)
    {
        var results = await _service.QueryAsync(accountId, cancellationToken);
        return Ok(results);
    }

    [HttpGet("{id}", Name = "GetTransactionById")]
    public async Task<ActionResult<TransactionResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var tx = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(tx);
    }

    [HttpPost]
    public async Task<ActionResult<TransactionResponse>> Create([FromBody] TransactionRequest request, CancellationToken cancellationToken)
    {
        var created = await _service.ExecuteAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
