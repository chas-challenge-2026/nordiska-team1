using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nordiska.BuildingBlocks.Database;
using Nordiska.Modules.Faq.Application;
using Nordiska.Modules.Faq.Contracts.Requests;
using Nordiska.Modules.Faq.Contracts.Responses;

namespace Nordiska.FrontendApi.Controllers;

/// <summary>
/// API endpoints for managing and querying FAQ knowledge base entries.
/// </summary>
[ApiController]
[Route("api/faqs")]
[Route("api/faq")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class FaqController(FaqService service) : ControllerBase
{
    /// <summary>
    /// Retrieves paginated FAQ entries for a specific language with optional search, category, and keyword filters.
    /// </summary>
    /// <param name="lang">The language code (e.g. 'sv', 'en').</param>
    /// <param name="page">Page index (1-based, default 1).</param>
    /// <param name="pageSize">Page size (default 20, max 100).</param>
    /// <param name="search">Free text search term across question, answer, and keywords.</param>
    /// <param name="category">Category filter (e.g. 'Ränta', 'Interest').</param>
    /// <param name="keyword">Keyword filter (e.g. 'ränta', 'deposit').</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Paginated list of FAQ entries matching the criteria.</response>
    /// <response code="400">Validation failed for query parameters.</response>
    [HttpGet("{lang:regex(^(sv|en)$)}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<FaqEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<FaqEntryResponse>>> GetByLanguage(
        [FromRoute] string lang,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new FaqQueryParameters(
            Page: page,
            PageSize: pageSize,
            SearchTerm: search,
            Category: category,
            Keyword: keyword,
            Lang: lang);

        var result = await service.GetByLanguagePagedAsync(parameters, GetSessionKey(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves FAQ searches that gave no results, most searched first. Requires the faq:manage permission.
    /// </summary>
    /// <remarks>
    /// Searches are saved in the background, so a search made just now can take a moment to show up.
    /// The same search from the same session is only counted once per 15 minutes.
    /// </remarks>
    /// <param name="lang">Optional language filter ('sv' or 'en').</param>
    /// <param name="days">How many days back to look (default 30, max 90).</param>
    /// <param name="limit">Max number of searches to return (default 50, max 200).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Searches without results, grouped by normalized query and language.</response>
    /// <response code="400">Validation failed for query parameters.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="403">Forbidden if the user lacks the faq:manage policy.</response>
    [HttpGet("analytics/content-gaps")]
    [Authorize(Policy = "faq:manage")]
    [ProducesResponseType(typeof(IReadOnlyList<FaqContentGapResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<FaqContentGapResponse>>> GetContentGaps(
        [FromQuery, RegularExpression("^(sv|en)$")] string? lang = null,
        [FromQuery, Range(1, 90)] int days = 30,
        [FromQuery, Range(1, 200)] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var gaps = await service.GetContentGapsAsync(lang, days, limit, cancellationToken);
        return Ok(gaps);
    }

    /// <summary>
    /// Retrieves distinct FAQ categories available for a specific language.
    /// </summary>
    /// <param name="lang">The language code (e.g. 'sv', 'en').</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">List of distinct category names for the language.</response>
    [HttpGet("{lang:regex(^(sv|en)$)}/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetCategories(
        [FromRoute] string lang,
        CancellationToken cancellationToken = default)
    {
        var categories = await service.GetCategoriesAsync(lang, cancellationToken);
        return Ok(categories);
    }

    /// <summary>
    /// Increases the helpfulness count of an FAQ entry by 1.
    /// </summary>
    /// <param name="id">The unique identifier of the FAQ entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">FAQ entry with updated helpfulness count.</response>
    /// <response code="404">FAQ entry not found.</response>
    [HttpPost("{id:int}/increase")]
    [HttpPatch("{id:int}/increase")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FaqEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FaqEntryResponse>> IncreaseHelpful(
        [FromRoute, Range(1, int.MaxValue)] int id,
        CancellationToken cancellationToken)
    {
        var updated = await service.AdjustHelpfulAsync(id, 1, cancellationToken);
        if (updated is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "FAQ entry not found.");
        }

        return Ok(updated);
    }

    /// <summary>
    /// Decreases the helpfulness count of an FAQ entry by 1 (supports negative count for downvotes).
    /// </summary>
    /// <param name="id">The unique identifier of the FAQ entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">FAQ entry with updated helpfulness count.</response>
    /// <response code="404">FAQ entry not found.</response>
    [HttpPost("{id:int}/decrease")]
    [HttpPatch("{id:int}/decrease")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FaqEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FaqEntryResponse>> DecreaseHelpful(
        [FromRoute, Range(1, int.MaxValue)] int id,
        CancellationToken cancellationToken)
    {
        var updated = await service.AdjustHelpfulAsync(id, -1, cancellationToken);
        if (updated is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "FAQ entry not found.");
        }

        return Ok(updated);
    }

    /// <summary>
    /// Partially updates an FAQ entry. Requires the faq:manage permission.
    /// </summary>
    /// <param name="id">The unique identifier of the FAQ entry.</param>
    /// <param name="request">The partial update payload containing fields to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">FAQ entry updated successfully.</response>
    /// <response code="400">Validation failed on the provided data.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="403">Forbidden if the user lacks the faq:manage policy.</response>
    /// <response code="404">FAQ entry not found.</response>
    [HttpPatch("{id:int}")]
    [Authorize(Policy = "faq:manage")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(FaqEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FaqEntryResponse>> Patch(
        [FromRoute, Range(1, int.MaxValue)] int id,
        [FromBody] PatchFaqRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await service.PatchAsync(id, request, cancellationToken);
        if (updated is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "FAQ entry not found.");
        }

        return Ok(updated);
    }

    /// <summary>
    /// Partially updates an FAQ entry by language and ID. Requires the faq:manage permission.
    /// </summary>
    /// <param name="lang">The language code (e.g. 'sv', 'en').</param>
    /// <param name="id">The unique identifier of the FAQ entry.</param>
    /// <param name="request">The partial update payload containing fields to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">FAQ entry updated successfully.</response>
    /// <response code="400">Validation failed on the provided data.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="403">Forbidden if the user lacks the faq:manage policy.</response>
    /// <response code="404">FAQ entry not found.</response>
    [HttpPatch("{lang:alpha}/{id:int}")]
    [Authorize(Policy = "faq:manage")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(FaqEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FaqEntryResponse>> PatchWithLang(
        [FromRoute] string lang,
        [FromRoute, Range(1, int.MaxValue)] int id,
        [FromBody] PatchFaqRequest request,
        CancellationToken cancellationToken)
    {
        var patchReq = request.Lang is null ? request with { Lang = lang } : request;
        return await Patch(id, patchReq, cancellationToken);
    }

    /// <summary>
    /// Creates a new FAQ entry. Requires the faq:manage policy (Admin role, e.g. login as admin@nordiska.se).
    /// </summary>
    /// <param name="request">The FAQ creation payload containing question, answer, category, keywords, and lang.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="201">FAQ entry created successfully.</response>
    /// <response code="400">Validation failed on the provided FAQ data.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="403">Forbidden if the user lacks the Admin role / faq:manage policy.</response>
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
            Keywords = request.Keywords?.Trim(),
            Lang = request.Lang?.Trim()
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
            normalized.Lang ?? "sv",
            normalized.RelationId,
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

    [HttpGet("relation/{relationId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(
        typeof(IReadOnlyCollection<FaqEntryResponse>),
        StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<FaqEntryResponse>>> GetByRelationId(
        [FromRoute] Guid relationId,
        CancellationToken cancellationToken)
    {
        var entries = await service.GetByRelationIdAsync(
            relationId,
            cancellationToken);

        return Ok(entries);
    }

    /// <summary>
    /// Retrieves the related articles of an FAQ article, in the order they're shown. Requires the faq:manage permission.
    /// </summary>
    /// <remarks>
    /// Only returns the related articles set by an admin, not the popular fallback.
    /// </remarks>
    /// <param name="relationId">The RelationId shared by the language versions of the article.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">RelationIds of the related articles, empty if none are set.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="403">Forbidden if the user lacks the faq:manage policy.</response>
    /// <response code="404">FAQ article not found.</response>
    [HttpGet("relation/{relationId:guid}/related")]
    [Authorize(Policy = "faq:manage")]
    [ProducesResponseType(typeof(IReadOnlyList<Guid>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<Guid>>> GetRelated(
        [FromRoute] Guid relationId,
        CancellationToken cancellationToken)
    {
        var related = await service.GetRelatedRelationIdsAsync(relationId, cancellationToken);
        if (related is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "FAQ entry not found.");
        }

        return Ok(related);
    }

    /// <summary>
    /// Sets the related articles of an FAQ article, replacing the current ones. Requires the faq:manage permission.
    /// </summary>
    /// <remarks>
    /// Relations are one-way and cover every language version of the articles.
    /// Send an empty list to go back to the popular articles in the same category.
    /// </remarks>
    /// <param name="relationId">The RelationId shared by the language versions of the article.</param>
    /// <param name="request">RelationIds of the related articles in the order they should be shown (max 5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="204">Related articles saved.</response>
    /// <response code="400">Too many, duplicate, unknown or self-referencing related articles.</response>
    /// <response code="401">Unauthorized if authentication token is missing or invalid.</response>
    /// <response code="403">Forbidden if the user lacks the faq:manage policy.</response>
    /// <response code="404">FAQ article not found.</response>
    [HttpPut("relation/{relationId:guid}/related")]
    [Authorize(Policy = "faq:manage")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetRelated(
        [FromRoute] Guid relationId,
        [FromBody] SetRelatedFaqsRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await service.SetRelatedAsync(relationId, request.RelatedRelationIds, cancellationToken);
        if (!updated)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "FAQ entry not found.");
        }

        return NoContent();
    }

    /// <summary>
    /// Registers that an FAQ article was opened. Used to find the most popular articles in a category.
    /// </summary>
    /// <remarks>
    /// Views are saved in the background. The same article opened from the same session is only counted once per 15 minutes.
    /// </remarks>
    /// <param name="id">The unique identifier of the FAQ entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="202">View registered.</response>
    /// <response code="404">FAQ entry not found.</response>
    [HttpPost("{id:int}/view")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegisterView(
        [FromRoute, Range(1, int.MaxValue)] int id,
        CancellationToken cancellationToken)
    {
        var found = await service.RegisterViewAsync(id, GetSessionKey(), cancellationToken);
        if (!found)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "FAQ entry not found.");
        }

        return Accepted();
    }

    /// <summary>
    /// Retrieves an FAQ entry by language and ID, with its related articles in the same language.
    /// </summary>
    /// <param name="lang">The language code (e.g. 'sv', 'en').</param>
    /// <param name="id">The unique identifier of the FAQ entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">FAQ entry retrieved successfully.</response>
    /// <response code="400">Invalid identifier.</response>
    /// <response code="404">FAQ entry not found in this language.</response>
    [HttpGet("{lang:regex(^(sv|en)$)}/{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FaqEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FaqEntryResponse>> GetByLanguageAndId(
        [FromRoute] string lang,
        [FromRoute, Range(1, int.MaxValue)] int id,
        CancellationToken cancellationToken)
    {
        var entry = await service.GetWithRelatedAsync(id, lang, cancellationToken);
        if (entry is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "FAQ entry not found.");
        }

        return Ok(entry);
    }

    /// <summary>
    /// Retrieves an FAQ entry by its ID, with its related articles in the same language.
    /// </summary>
    /// <remarks>
    /// Related articles are the ones set by an admin. If none are set, the 3 most viewed articles
    /// in the same category over the last 30 days are returned instead.
    /// </remarks>
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
        var entry = await service.GetWithRelatedAsync(
            id,
            null,
            cancellationToken);

        if (entry is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "FAQ entry not found.");
        }

        return Ok(entry);
    }

    /// <summary>
    /// Searches FAQ entries across categories and keywords.
    /// </summary>
    [HttpGet("search")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyCollection<FaqEntryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<FaqEntryResponse>>> Search(
        [FromQuery] SearchFaqRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.SearchAsync(request, cancellationToken);
        return Ok(result);
    }

    // Search and views are anonymous so there is no real session, IP + browser is close enough to tell users apart.
    // It's hashed with a salt before it is stored.
    private string GetSessionKey()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();
        return $"{ip}|{userAgent}";
    }
}
