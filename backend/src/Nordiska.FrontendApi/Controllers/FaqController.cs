using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Contracts.Requests;
using Nordiska.Modules.Faq.Contracts.Responses;

namespace Nordiska.FrontendApi.Controllers;

[ApiController]
[Route("api/faqs")]
//[Authorize(Policy = "faq:manage")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails),StatusCodes.Status500InternalServerError)]
public sealed class FaqController(FaqService service) : ControllerBase
{


    [HttpPost]
    [Consumes("application/json")]
    [RequestSizeLimit(32 * 1024)]
    [ProducesResponseType(
    typeof(FaqCreatedResponse),
    StatusCodes.Status201Created)]
    [ProducesResponseType(
    typeof(ValidationProblemDetails),
    StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<FaqCreatedResponse>> Create
    (
    [FromBody] CreateFaqRequest request,
    CancellationToken cancellationToken
    )
    {
        var normalized = request with
        {
            Question = request.Question.Trim(),
            Answer = request.Answer.Trim(),
            Category = request.Category?.Trim(),
            Keywords = request.Keywords?.Trim()
        };
        ModelState.Clear();

        if (!TryValidateModel(normalized))
        {
            return ValidationProblem(ModelState);
        }

        var id = await service.CreateAsync(
            normalized.Question,
            normalized.Answer,
            normalized.Category,
            normalized.Keywords,
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            new FaqCreatedResponse(id));

    }


    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(
        typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute, Range(1, int.MaxValue)] int id,
        CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(
            id,
            cancellationToken);

        if (!deleted)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "FAQ entry not found.");
        }

        return NoContent();
    }


    [HttpGet("{id:int}")]
    [ProducesResponseType(
    typeof(FaqEntryResponse),
    StatusCodes.Status200OK)]
    [ProducesResponseType(
    typeof(ValidationProblemDetails),
    StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FaqEntryResponse>> GetById(
        [FromRoute, Range(1, int.MaxValue)] int id,
        CancellationToken cancellationToken)
    {
        var entry = await service.GetByIdAsync(
            id,
            cancellationToken);

        if (entry is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "FAQ entry not found.");
        }

        return Ok(entry);
    }

}