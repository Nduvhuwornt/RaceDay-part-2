using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[Route("api/users/me")]
[SessionAuthorize]
public class UsersController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public UsersController(RaceDayDbContext db) => _db = db;

    /// <summary>Returns the profile of the logged-in user (Organiser or Participant).</summary>
    /// <response code="200">The user's profile.</response>
    /// <response code="401">No active session.</response>
    [HttpGet]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe()
    {
        var user = await _db.Users.AsNoTracking().Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.UserId == CurrentUserId);

        if (user is null)
        {
            HttpContext.Session.Clear();
            return ProblemResult(StatusCodes.Status401Unauthorized, "Unauthorized", "Your session is no longer valid. Please log in again.");
        }

        return Ok(new UserProfileDto(user.UserId, user.FullName, user.Email, user.PhoneNumber, user.ProfilePictureUrl, user.Role.RoleName));
    }

    /// <summary>Updates the logged-in user's own profile details.</summary>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">No active session.</response>
    [HttpPut]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMe(UpdateProfileRequest request)
    {
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null)
        {
            HttpContext.Session.Clear();
            return ProblemResult(StatusCodes.Status401Unauthorized, "Unauthorized", "Your session is no longer valid. Please log in again.");
        }

        user.FullName = request.FullName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();
        user.ProfilePictureUrl = request.ProfilePictureUrl?.Trim();
        await _db.SaveChangesAsync();

        return Ok(new UserProfileDto(user.UserId, user.FullName, user.Email, user.PhoneNumber, user.ProfilePictureUrl, user.Role.RoleName));
    }
}
