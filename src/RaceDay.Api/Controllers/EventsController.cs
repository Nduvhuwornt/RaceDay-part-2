using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[Route("api/events")]
[SessionAuthorize]
public class EventsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db) => _db = db;

    /// <summary>Lists events. Available to both roles.</summary>
    /// <remarks>
    /// Optional filters: <c>type</c> (Run/Walk/Cycle), <c>search</c> (name or location),
    /// <c>upcomingOnly</c> (default true) and <c>mine</c> (only events created by the logged-in Organiser).
    /// </remarks>
    /// <response code="200">Array of events ordered by date.</response>
    /// <response code="401">No active session.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(
        [FromQuery] EventType? type,
        [FromQuery] string? search,
        [FromQuery] bool upcomingOnly = true,
        [FromQuery] bool mine = false)
    {
        var query = _db.Events.AsNoTracking().AsQueryable();

        if (upcomingOnly)
        {
            var now = DateTime.UtcNow;
            query = query.Where(e => e.EventDate >= now);
        }

        if (type.HasValue)
        {
            query = query.Where(e => e.EventType == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.Name.Contains(term) || e.Location.Contains(term));
        }

        if (mine)
        {
            var userId = CurrentUserId;
            query = query.Where(e => e.OrganiserId == userId);
        }

        var events = await query
            .OrderBy(e => e.EventDate)
            .Select(e => new EventSummaryDto(
                e.EventId, e.OrganiserId, e.Organiser.FullName, e.Name, e.Description, e.EventDate,
                e.Location, e.DistanceKm, e.EventType, e.BannerImageUrl, e.Enrolments.Count))
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>Returns full detail for one event, including its categories.</summary>
    /// <response code="200">Event detail.</response>
    /// <response code="401">No active session.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await _db.Events.AsNoTracking()
            .Include(e => e.Organiser)
            .Include(e => e.Categories)
            .SingleOrDefaultAsync(e => e.EventId == id);

        if (ev is null)
        {
            return NotFoundProblem($"Event {id} does not exist.");
        }

        var enrolmentCount = await _db.EventEnrolments.CountAsync(x => x.EventId == id);
        var categories = ev.Categories.OrderBy(c => c.Name).Select(CategoryDto.From).ToList();

        return Ok(new EventDetailDto(
            ev.EventId, ev.OrganiserId, ev.Organiser.FullName, ev.Name, ev.Description, ev.EventDate,
            ev.Location, ev.DistanceKm, ev.EventType, ev.BannerImageUrl, enrolmentCount, categories));
    }

    /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
    /// <response code="201">Event created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Only Organisers can create events.</response>
    [HttpPost]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(EventRequest request)
    {
        var entity = new RaceEvent
        {
            OrganiserId = CurrentUserId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            EventDate = request.EventDate!.Value,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm,
            EventType = request.EventType!.Value
        };

        _db.Events.Add(entity);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.EventId }, await ToSummaryAsync(entity));
    }

    /// <summary>Updates an event owned by the logged-in Organiser.</summary>
    /// <response code="200">Event updated.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpPut("{id:int}")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(EventSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, EventRequest request)
    {
        var entity = await _db.Events.SingleOrDefaultAsync(e => e.EventId == id);
        if (entity is null)
        {
            return NotFoundProblem($"Event {id} does not exist.");
        }

        if (entity.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only update events that you created.");
        }

        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.EventDate = request.EventDate!.Value;
        entity.Location = request.Location.Trim();
        entity.DistanceKm = request.DistanceKm;
        entity.EventType = request.EventType!.Value;
        await _db.SaveChangesAsync();

        return Ok(await ToSummaryAsync(entity));
    }

    /// <summary>Deletes an event owned by the logged-in Organiser, along with its categories, enrolments and results.</summary>
    /// <response code="204">Event deleted.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.Events.SingleOrDefaultAsync(e => e.EventId == id);
        if (entity is null)
        {
            return NotFoundProblem($"Event {id} does not exist.");
        }

        if (entity.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only delete events that you created.");
        }

        // Foreign keys are NO ACTION (as in the Part 1 SQL script), so remove dependants explicitly.
        var enrolments = await _db.EventEnrolments.Include(x => x.Result).Where(x => x.EventId == id).ToListAsync();
        _db.Results.RemoveRange(enrolments.Where(x => x.Result != null).Select(x => x.Result!));
        _db.EventEnrolments.RemoveRange(enrolments);
        var categories = await _db.Categories.Where(c => c.EventId == id).ToListAsync();
        _db.Categories.RemoveRange(categories);
        _db.Events.Remove(entity);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<EventSummaryDto> ToSummaryAsync(RaceEvent e)
    {
        var organiserName = await _db.Users.Where(u => u.UserId == e.OrganiserId).Select(u => u.FullName).SingleAsync();
        var count = await _db.EventEnrolments.CountAsync(x => x.EventId == e.EventId);
        return new EventSummaryDto(e.EventId, e.OrganiserId, organiserName, e.Name, e.Description, e.EventDate,
            e.Location, e.DistanceKm, e.EventType, e.BannerImageUrl, count);
    }
}
