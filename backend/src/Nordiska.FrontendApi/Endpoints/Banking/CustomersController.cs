using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Nordiska.Modules.Banking.Application;
using Nordiska.Modules.Banking.Contracts.Requests;
using Nordiska.FrontendApi.Contracts.Mappers;
using Nordiska.FrontendApi.Contracts.Requests;

namespace Nordiska.FrontendApi.Endpoints.Banking;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;

    public CustomersController(ICustomerService service)
    {
        _service = service;
    }
    /// <summary>
    /// Retrieves a customer by id.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>CustomerResponse</returns>
    [HttpGet("{id}", Name = "GetCustomerById")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var customer = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(customer.ToResponse());
    }
    /// <summary>
    ///     Creates a new customer.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerResponse>> Create([FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var created = await _service.CreateAsync(request.Name, request.Email, request.PersonalNum, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToResponse());
    }
    /// <summary>
    /// Updates a customer.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>CustomerResponse</returns>
    [HttpPut]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> Update([FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var updated = await _service.UpdateAsync(request.Id, request.Name, request.Email, request.PersonalNum, cancellationToken);
        return Ok(updated.ToResponse());
    }
}
