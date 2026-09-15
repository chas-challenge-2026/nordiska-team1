using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Contracts.Requests;
using Nordiska.Modules.Faq.Contracts.Responses;

namespace Nordiska.FrontendApi.Controllers;

/// <summary>
/// API endpoints for managing and querying FAQ knowledge base entries.
/// </summary>
[ApiController]
[Route("api/faqs")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class FaqController(FaqService service) : ControllerBase
{
    /// <summary>
    /// Creates a new FAQ entry. Requires the faq:manage permission.
    /// </summary>
    /// <param name="request">The FAQ creation payload containing question, answer, category, and keywords.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">FAQ entry created successfully.</response>
    /// <response code="400">Validation failed on the provided FAQ data.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="403">Forbidden if the user lacks the faq:manage policy.</response>
    /// <response code="413">Payload exceeds size limit.</response>
    /// <response code="415">Unsupported media type.</response>
    [HttpPost]
    [Authorize(Policy = "faq:manage")]
    [Consumes("application/json")]
    [RequestSizeLimit(32 * 1024)]
    [ProducesResponseType(typeof(FaqCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<FaqCreatedResponse>> Create(
        [FromBody] CreateFaqRequest request,
        CancellationToken cancellationToken)
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

    /// <summary>
    /// Deletes an FAQ entry by its ID. Requires the faq:manage permission.
    /// </summary>
    /// <param name="id">The unique identifier of the FAQ entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">FAQ entry deleted successfully.</response>
    /// <response code="400">Invalid identifier.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="403">Forbidden if the user lacks the faq:manage policy.</response>
    /// <response code="404">FAQ entry not found.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "faq:manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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

    /// <summary>
    /// Retrieves an FAQ entry by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the FAQ entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">FAQ entry retrieved successfully.</response>
    /// <response code="400">Invalid identifier.</response>
    /// <response code="404">FAQ entry not found.</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FaqEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
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