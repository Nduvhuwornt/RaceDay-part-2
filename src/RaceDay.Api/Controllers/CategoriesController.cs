using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[SessionAuthorize]
public class CategoriesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db) => _db = db;

    /// <summary>Lists the age/distance categories defined for an event.</summary>
    /// <response code="200">Array of categories.</response>
    /// <response code="401">No active session.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
        {
            return NotFoundProblem($"Event {eventId} does not exist.");
        }

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.CategoryId, c.EventId, c.Name, c.MinAge, c.MaxAge, c.DistanceKm))
            .ToListAsync();

        return Ok(categories);
    }

    /// <summary>Adds an age or distance category to an event owned by the logged-in Organiser.</summary>
    /// <response code="201">Category created.</response>
    /// <response code="400">Validation failed (e.g. MinAge greater than MaxAge).</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPost("api/events/{eventId:int}/categories")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int eventId, CategoryRequest request)
    {
        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null)
        {
            return NotFoundProblem($"Event {eventId} does not exist.");
        }

        if (ev.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only add categories to events that you created.");
        }

        if (request.MinAge.HasValue && request.MaxAge.HasValue && request.MinAge > request.MaxAge)
        {
            return BadRequestProblem("MinAge cannot be greater than MaxAge.");
        }

        var category = new Category
        {
            EventId = eventId,
            Name = request.Name.Trim(),
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,
            DistanceKm = request.DistanceKm
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return Created($"/api/events/{eventId}/categories", CategoryDto.From(category));
    }

    /// <summary>Updates a category on an event owned by the logged-in Organiser.</summary>
    /// <response code="200">Category updated.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns the event.</response>
    /// <response code="404">Category does not exist.</response>
    [HttpPut("api/categories/{id:int}")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category is null)
        {
            return NotFoundProblem($"Category {id} does not exist.");
        }

        if (category.Event.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only update categories on events that you created.");
        }

        if (request.MinAge.HasValue && request.MaxAge.HasValue && request.MinAge > request.MaxAge)
        {
            return BadRequestProblem("MinAge cannot be greater than MaxAge.");
        }

        category.Name = request.Name.Trim();
        category.MinAge = request.MinAge;
        category.MaxAge = request.MaxAge;
        category.DistanceKm = request.DistanceKm;
        await _db.SaveChangesAsync();

        return Ok(CategoryDto.From(category));
    }

    /// <summary>Removes a category from an event owned by the logged-in Organiser.</summary>
    /// <response code="204">Category deleted.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns the event.</response>
    /// <response code="404">Category does not exist.</response>
    /// <response code="409">Participants are already enrolled in this category.</response>
    [HttpDelete("api/categories/{id:int}")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category is null)
        {
            return NotFoundProblem($"Category {id} does not exist.");
        }

        if (category.Event.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only delete categories on events that you created.");
        }

        if (await _db.EventEnrolments.AnyAsync(x => x.CategoryId == id))
        {
            return ConflictProblem("Participants are already enrolled in this category, so it cannot be deleted.");
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
