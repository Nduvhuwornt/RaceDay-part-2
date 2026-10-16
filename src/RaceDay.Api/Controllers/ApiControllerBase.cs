using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Security;

namespace RaceDay.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>ID of the logged-in user, read from the server-side session.</summary>
    protected int CurrentUserId =>
        HttpContext.Session.GetUserId() ?? throw new InvalidOperationException("No user in session.");

    protected string CurrentRole => HttpContext.Session.GetRole() ?? string.Empty;

    protected ObjectResult ProblemResult(int status, string title, string detail) =>
        StatusCode(status, new ProblemDetails { Status = status, Title = title, Detail = detail });

    protected ObjectResult NotFoundProblem(string detail) => ProblemResult(StatusCodes.Status404NotFound, "Not Found", detail);
    protected ObjectResult ForbiddenProblem(string detail) => ProblemResult(StatusCodes.Status403Forbidden, "Forbidden", detail);
    protected ObjectResult ConflictProblem(string detail) => ProblemResult(StatusCodes.Status409Conflict, "Conflict", detail);
    protected ObjectResult BadRequestProblem(string detail) => ProblemResult(StatusCodes.Status400BadRequest, "Bad Request", detail);
}
