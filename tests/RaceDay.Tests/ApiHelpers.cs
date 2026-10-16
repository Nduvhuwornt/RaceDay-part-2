using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Tests;

public static class ApiHelpers
{
    public const string Password = "Passw0rd!";

    /// <summary>Registers a fresh user with the given role, logs in, and returns a client holding the session cookie.</summary>
    public static async Task<(HttpClient Client, string Email)> CreateLoggedInClientAsync(RaceDayApiFactory factory, string role)
    {
        var client = factory.CreateClient();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.co.za";

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = $"Test {role}",
            email,
            password = Password,
            phoneNumber = "0821234567",
            role,
            organiserCode = role == "Organiser" ? RaceDayApiFactory.OrganiserCode : null
        });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();

        return (client, email);
    }

    public static object EventBody(DateTime? date = null, string name = "Test Fun Run") => new
    {
        name,
        description = "A test event",
        eventDate = date ?? DateTime.UtcNow.AddDays(30),
        location = "Pretoria, Gauteng",
        distanceKm = 10.0,
        eventType = "Run"
    };

    public static async Task<int> ReadIntAsync(HttpResponseMessage response, string property)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty(property).GetInt32();
    }

    public static async Task<int> CreateEventAsync(HttpClient organiser, DateTime? date = null)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", EventBody(date));
        response.EnsureSuccessStatusCode();
        return await ReadIntAsync(response, "eventId");
    }

    public static async Task<int> CreateCategoryAsync(HttpClient organiser, int eventId, string name = "Senior")
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name, minAge = 20, maxAge = 59, distanceKm = 10.0 });
        response.EnsureSuccessStatusCode();
        return await ReadIntAsync(response, "categoryId");
    }

    public static async Task<int> EnrolAsync(HttpClient participant, int eventId, int categoryId)
    {
        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });
        response.EnsureSuccessStatusCode();
        return await ReadIntAsync(response, "enrolmentId");
    }
}
