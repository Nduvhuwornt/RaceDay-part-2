using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[SessionAuthorize]
public class ResultsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Captures the finish time and finishing position for a Participant's enrolment.</summary>
    /// <response code="201">Result captured.</response>
    /// <response code="400">Validation failed, or the event has not taken place yet.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">Enrolment does not exist.</response>
    /// <response code="409">A result is already captured for this enrolment, or the position is already taken.</response>
    [HttpPost("api/enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Capture(int enrolmentId, ResultRequest request)
    {
        var enrolment = await _db.EventEnrolments.Include(x => x.Event)
            .SingleOrDefaultAsync(x => x.EnrolmentId == enrolmentId);
        if (enrolment is null)
        {
            return NotFoundProblem($"Enrolment {enrolmentId} does not exist.");
        }

        if (enrolment.Event.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only capture results for events that you created.");
        }

        if (enrolment.Event.EventDate > DateTime.UtcNow)
        {
            return BadRequestProblem("Results can only be captured after the event has taken place.");
        }

        if (request.FinishTime!.Value <= TimeSpan.Zero)
        {
            return BadRequestProblem("FinishTime must be greater than zero.");
        }

        if (await _db.Results.AnyAsync(r => r.EnrolmentId == enrolmentId))
        {
            return ConflictProblem("A result has already been captured for this enrolment.");
        }

        var eventId = enrolment.EventId;
        if (await _db.Results.AnyAsync(r => r.Enrolment.EventId == eventId && r.FinishPosition == request.FinishPosition))
        {
            return ConflictProblem($"Finishing position {request.FinishPosition} has already been assigned in this event.");
        }

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            CapturedByOrganiserId = CurrentUserId,
            FinishTime = request.FinishTime.Value,
            FinishPosition = request.FinishPosition
        };

        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var dto = (await ToDtosAsync(_db.Results.AsNoTracking().Where(r => r.ResultId == result.ResultId))).Single();
        return Created($"/api/events/{eventId}/results", dto);
    }

    /// <summary>Returns the logged-in Participant's personal race history.</summary>
    /// <response code="200">Results across all events the Participant finished, newest first.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Only Participants have personal results.</response>
    [HttpGet("api/users/me/results")]
    [SessionAuthorize(Roles = RoleNames.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var participantId = CurrentUserId;
        var results = await ToDtosAsync(_db.Results.AsNoTracking().Where(r => r.Enrolment.ParticipantId == participantId));
        return Ok(results.OrderByDescending(r => r.EventDate).ToList());
    }

    /// <summary>Returns all captured results for an event owned by the logged-in Organiser.</summary>
    /// <response code="200">Results ordered by finishing position.</response>
    /// <response code="401">No active session.</response>
    /// <response code="403">Not an Organiser, or not the Organiser who owns this event.</response>
    /// <response code="404">Event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/results")]
    [SessionAuthorize(Roles = RoleNames.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
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
            return ForbiddenProblem("You can only view results for events that you created.");
        }

        var results = await ToDtosAsync(_db.Results.AsNoTracking().Where(r => r.Enrolment.EventId == eventId));
        return Ok(results.OrderBy(r => r.FinishPosition).ToList());
    }

    private async Task<List<ResultDto>> ToDtosAsync(IQueryable<Result> query)
    {
        var rows = await query.Select(r => new
        {
            r.ResultId,
            r.EnrolmentId,
            EventId = r.Enrolment.EventId,
            EventName = r.Enrolment.Event.Name,
            EventDate = r.Enrolment.Event.EventDate,
            CategoryName = r.Enrolment.Category.Name,
            ParticipantId = r.Enrolment.ParticipantId,
            ParticipantName = r.Enrolment.Participant.FullName,
            r.FinishTime,
            r.FinishPosition,
            r.CapturedDate
        }).ToListAsync();

        var eventIds = rows.Select(r => r.EventId).Distinct().ToList();
        var finisherCounts = await _db.Results
            .Where(r => eventIds.Contains(r.Enrolment.EventId))
            .GroupBy(r => r.Enrolment.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.EventId, g => g.Count);

        return rows.Select(r => new ResultDto(
            r.ResultId, r.EnrolmentId, r.EventId, r.EventName, r.EventDate, r.CategoryName,
            r.ParticipantId, r.ParticipantName, r.FinishTime, r.FinishPosition,
            finisherCounts.GetValueOrDefault(r.EventId), r.CapturedDate)).ToList();
    }
}
