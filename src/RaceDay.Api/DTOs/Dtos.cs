using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

// ---------- Auth ----------
public class RegisterRequest
{
    /// <example>Thandiwe Nkosi</example>
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    /// <example>thandiwe.nkosi@gmail.com</example>
    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    /// <summary>At least 8 characters. Stored as a salted PBKDF2 hash, never in plain text.</summary>
    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    /// <example>0721112222</example>
    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>Either "Organiser" or "Participant".</summary>
    /// <example>Participant</example>
    [Required]
    public string Role { get; set; } = string.Empty;

    /// <summary>Secret organiser registration code. Required only when Role is "Organiser".</summary>
    public string? OrganiserCode { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public record AuthResponse(int UserId, string FullName, string Email, string Role);

// ---------- Profile ----------
public record UserProfileDto(int UserId, string FullName, string Email, string? PhoneNumber, string? ProfilePictureUrl, string Role);

public class UpdateProfileRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [StringLength(300)]
    public string? ProfilePictureUrl { get; set; }
}

// ---------- Events ----------
public class EventRequest
{
    [Required, StringLength(150, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateTime? EventDate { get; set; }

    [Required, StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [Range(0.1, 1000)]
    public decimal DistanceKm { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    [Required]
    public EventType? EventType { get; set; }
}

public record EventSummaryDto(
    int EventId,
    int OrganiserId,
    string OrganiserName,
    string Name,
    string? Description,
    DateTime EventDate,
    string Location,
    decimal DistanceKm,
    EventType EventType,
    string? BannerImageUrl,
    int EnrolmentCount);

public record EventDetailDto(
    int EventId,
    int OrganiserId,
    string OrganiserName,
    string Name,
    string? Description,
    DateTime EventDate,
    string Location,
    decimal DistanceKm,
    EventType EventType,
    string? BannerImageUrl,
    int EnrolmentCount,
    List<CategoryDto> Categories);

// ---------- Categories ----------
public class CategoryRequest
{
    /// <example>Under 20</example>
    [Required, StringLength(50, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Range(0, 120)]
    public int? MinAge { get; set; }

    [Range(0, 120)]
    public int? MaxAge { get; set; }

    [Range(0.1, 1000)]
    public decimal? DistanceKm { get; set; }
}

public record CategoryDto(int CategoryId, int EventId, string Name, int? MinAge, int? MaxAge, decimal? DistanceKm)
{
    public static CategoryDto From(Category c) => new(c.CategoryId, c.EventId, c.Name, c.MinAge, c.MaxAge, c.DistanceKm);
}

// ---------- Enrolments ----------
public class EnrolRequest
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}

/// <summary>A participant's own view of an enrolment.</summary>
public record EnrolmentDto(
    int EnrolmentId,
    int EventId,
    string EventName,
    DateTime EventDate,
    string Location,
    int CategoryId,
    string CategoryName,
    DateTime EnrolmentDate,
    string Status);

/// <summary>An organiser's view of who has enrolled in their event.</summary>
public record EventEnrolmentDto(
    int EnrolmentId,
    int ParticipantId,
    string ParticipantName,
    string ParticipantEmail,
    int CategoryId,
    string CategoryName,
    DateTime EnrolmentDate,
    string Status,
    bool HasResult);

// ---------- Results ----------
public class ResultRequest
{
    /// <summary>Finish time as hh:mm:ss, e.g. 00:52:14.</summary>
    /// <example>00:52:14</example>
    [Required]
    public TimeSpan? FinishTime { get; set; }

    /// <example>47</example>
    [Range(1, 1_000_000)]
    public int FinishPosition { get; set; }
}

public record ResultDto(
    int ResultId,
    int EnrolmentId,
    int EventId,
    string EventName,
    DateTime EventDate,
    string CategoryName,
    int ParticipantId,
    string ParticipantName,
    TimeSpan FinishTime,
    int FinishPosition,
    int TotalFinishers,
    DateTime CapturedDate);
