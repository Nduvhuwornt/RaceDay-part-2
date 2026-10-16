using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RaceDay.Api.Models;

public static class RoleNames
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EventType
{
    Run,
    Walk,
    Cycle
}

public class Role
{
    [Key]
    public int RoleId { get; set; }

    [Required, MaxLength(20)]
    public string RoleName { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}

public class User
{
    [Key]
    public int UserId { get; set; }

    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(300)]
    public string? ProfilePictureUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RaceEvent> OrganisedEvents { get; set; } = new List<RaceEvent>();
    public ICollection<EventEnrolment> Enrolments { get; set; } = new List<EventEnrolment>();
    public ICollection<Result> CapturedResults { get; set; } = new List<Result>();
}

public class RaceEvent
{
    [Key]
    public int EventId { get; set; }

    public int OrganiserId { get; set; }
    public User Organiser { get; set; } = null!;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime EventDate { get; set; }

    [Required, MaxLength(150)]
    public string Location { get; set; } = string.Empty;

    public decimal DistanceKm { get; set; }

    public EventType EventType { get; set; }

    [MaxLength(300)]
    public string? BannerImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<EventEnrolment> Enrolments { get; set; } = new List<EventEnrolment>();
}

public class Category
{
    [Key]
    public int CategoryId { get; set; }

    public int EventId { get; set; }
    public RaceEvent Event { get; set; } = null!;

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal? DistanceKm { get; set; }

    public ICollection<EventEnrolment> Enrolments { get; set; } = new List<EventEnrolment>();
}

public class EventEnrolment
{
    [Key]
    public int EnrolmentId { get; set; }

    public int EventId { get; set; }
    public RaceEvent Event { get; set; } = null!;

    public int ParticipantId { get; set; }
    public User Participant { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(20)]
    public string Status { get; set; } = EnrolmentStatuses.Pending;

    public Result? Result { get; set; }
}

public class Result
{
    [Key]
    public int ResultId { get; set; }

    public int EnrolmentId { get; set; }
    public EventEnrolment Enrolment { get; set; } = null!;

    public int CapturedByOrganiserId { get; set; }
    public User CapturedBy { get; set; } = null!;

    public TimeSpan FinishTime { get; set; }
    public int FinishPosition { get; set; }
    public DateTime CapturedDate { get; set; } = DateTime.UtcNow;
}
