using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Security;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordService _passwords;
    private readonly IConfiguration _config;

    public AuthController(RaceDayDbContext db, IPasswordService passwords, IConfiguration config)
    {
        _db = db;
        _passwords = passwords;
        _config = config;
    }

    /// <summary>Registers a new user as an Organiser or a Participant.</summary>
    /// <remarks>
    /// Anyone can register as a Participant. To stop Participants promoting themselves, registering as an
    /// Organiser also requires the secret organiser code configured on the server (Registration:OrganiserCode).
    /// </remarks>
    /// <response code="201">User created. The password is stored as a salted hash.</response>
    /// <response code="400">Validation failed (e.g. invalid email, short password, unknown role).</response>
    /// <response code="403">Organiser registration code missing or incorrect.</response>
    /// <response code="409">Email address already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var requested = request.Role.Trim();
        string? roleName = requested.Equals(RoleNames.Organiser, StringComparison.OrdinalIgnoreCase) ? RoleNames.Organiser
            : requested.Equals(RoleNames.Participant, StringComparison.OrdinalIgnoreCase) ? RoleNames.Participant
            : null;

        if (roleName is null)
        {
            ModelState.AddModelError(nameof(request.Role), "Role must be either 'Organiser' or 'Participant'.");
            return ValidationProblem(ModelState);
        }

        if (roleName == RoleNames.Organiser)
        {
            var expectedCode = _config["Registration:OrganiserCode"];
            if (string.IsNullOrWhiteSpace(expectedCode) || request.OrganiserCode != expectedCode)
            {
                return ForbiddenProblem("A valid organiser registration code is required to register as an Organiser.");
            }
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return ConflictProblem("An account with this email address already exists.");
        }

        var role = await _db.Roles.SingleAsync(r => r.RoleName == roleName);
        var user = new User
        {
            RoleId = role.RoleId,
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = _passwords.Hash(request.Password),
            PhoneNumber = request.PhoneNumber?.Trim()
        };

        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ConflictProblem("An account with this email address already exists.");
        }

        return Created("/api/users/me", new AuthResponse(user.UserId, user.FullName, user.Email, roleName));
    }

    /// <summary>Logs in and starts a server-side session holding the user's ID and role.</summary>
    /// <response code="200">Login successful. A session cookie is returned.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.Email == email);

        if (user is null || !_passwords.Verify(request.Password, user.PasswordHash))
        {
            return ProblemResult(StatusCodes.Status401Unauthorized, "Unauthorized", "Invalid email or password.");
        }

        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role.RoleName);

        return Ok(new AuthResponse(user.UserId, user.FullName, user.Email, user.Role.RoleName));
    }

    /// <summary>Ends the current session.</summary>
    /// <response code="200">Logged out.</response>
    /// <response code="401">No active session.</response>
    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { message = "Logged out." });
    }
}
