using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[SessionAuthorize]
public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    /// <summary>Enrols the logged-in Participant in an event under the chosen category.</summary>
    /// <response code="201">Enrolment recorded (status Confirmed).</response>
    /// <response code="400">Validation failed, or the event has already taken place.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Only Participants can enrol.</response>
    /// <response code="404">Event does not exist, or the category does not belong to this event.</response>
    /// <response code="409">Already enrolled in this event.</response>
    [HttpPost("api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int eventId, EnrolRequest request)
    {
        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null)
        {
            return NotFoundProblem($"Event {eventId} does not exist.");
        }

        if (ev.EventDate < DateTime.UtcNow)
        {
            return BadRequestProblem("This event has already taken place, so enrolment is closed.");
        }

        var category = await _db.Categories.AsNoTracking()
            .SingleOrDefaultAsync(c => c.CategoryId == request.CategoryId && c.EventId == eventId);
        if (category is null)
        {
            return NotFoundProblem($"Category {request.CategoryId} does not exist for event {eventId}.");
        }

        var participantId = CurrentUserId;
        if (await _db.EventEnrolments.AnyAsync(x => x.EventId == eventId && x.ParticipantId == participantId))
        {
            return ConflictProblem("You are already enrolled in this event.");
        }

        var enrolment = new EventEnrolment
        {
            EventId = eventId,
            ParticipantId = participantId,
            CategoryId = category.CategoryId,
            Status = EnrolmentStatuses.Confirmed
        };

        _db.EventEnrolments.Add(enrolment);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ConflictProblem("You are already enrolled in this event.");
        }

        var dto = new EnrolmentDto(enrolment.EnrolmentId, ev.EventId, ev.Name, ev.EventDate, ev.Location,
            category.CategoryId, category.Name, enrolment.EnrolmentDate, enrolment.Status);

        return Created("/api/users/me/enrolments", dto);
    }

    /// <summary>Returns every event the logged-in Participant has enrolled in.</summary>
    /// <response code="200">Array of the Participant's enrolments, newest event first.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Only Participants have personal enrolments.</response>
    [HttpGet("api/users/me/enrolments")]
    [SessionAuthorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var participantId = CurrentUserId;
        var enrolments = await _db.EventEnrolments.AsNoTracking()
            .Where(x => x.ParticipantId == participantId)
            .OrderByDescending(x => x.Event.EventDate)
            .Select(x => new EnrolmentDto(x.EnrolmentId, x.EventId, x.Event.Name, x.Event.EventDate, x.Event.Location,
                x.CategoryId, x.Category.Name, x.EnrolmentDate, x.Status))
            .ToListAsync();

        return Ok(enrolments);
    }

    /// <summary>Lists everyone enrolled in an event owned by the logged-in Organiser.</summary>
    /// <response code="200">Array of enrolments with participant and category details.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EventEnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null)
        {
            return NotFoundProblem($"Event {eventId} does not exist.");
        }

        if (ev.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only view enrolments for events that you created.");
        }

        var enrolments = await _db.EventEnrolments.AsNoTracking()
            .Where(x => x.EventId == eventId)
            .OrderBy(x => x.EnrolmentDate)
            .Select(x => new EventEnrolmentDto(x.EnrolmentId, x.ParticipantId, x.Participant.FullName, x.Participant.Email,
                x.CategoryId, x.Category.Name, x.EnrolmentDate, x.Status, x.Result != null))
            .ToListAsync();

        return Ok(enrolments);
    }
}
