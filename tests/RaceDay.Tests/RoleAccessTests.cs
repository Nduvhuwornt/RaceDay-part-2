using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;

/// <summary>Verifies authentication (401) and role-based access (403) across the API.</summary>
public class RoleAccessTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public RoleAccessTests(RaceDayApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/events")]
    [InlineData("/api/events/1")]
    [InlineData("/api/events/1/categories")]
    [InlineData("/api/users/me")]
    [InlineData("/api/users/me/enrolments")]
    [InlineData("/api/users/me/results")]
    [InlineData("/api/events/1/enrolments")]
    [InlineData("/api/events/1/results")]
    public async Task ProtectedGetEndpoints_WithoutLogin_Return401(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithoutLogin_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/events", ApiHelpers.EventBody());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_Returns403()
    {
        var (participant, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");

        var response = await participant.PostAsJsonAsync("/api/events", ApiHelpers.EventBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdateOrDeleteEvent_Returns403()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (participant, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);

        var update = await participant.PutAsJsonAsync($"/api/events/{eventId}", ApiHelpers.EventBody());
        var delete = await participant.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotManageCategories_Returns403()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (participant, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiHelpers.CreateCategoryAsync(organiser, eventId);

        var create = await participant.PostAsJsonAsync($"/api/events/{eventId}/categories", new { name = "Sneaky" });
        var delete = await participant.DeleteAsync($"/api/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolmentsOrResults_Returns403()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (participant, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);

        var enrolments = await participant.GetAsync($"/api/events/{eventId}/enrolments");
        var results = await participant.GetAsync($"/api/events/{eventId}/results");

        Assert.Equal(HttpStatusCode.Forbidden, enrolments.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, results.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCaptureResult_Returns403()
    {
        var (participant, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");

        var response = await participant.PostAsJsonAsync("/api/enrolments/1/result", new { finishTime = "00:50:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrolInEvent_Returns403()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiHelpers.CreateCategoryAsync(organiser, eventId);

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/users/me/enrolments")]
    [InlineData("/api/users/me/results")]
    public async Task Organiser_CannotViewParticipantPersonalEndpoints_Returns403(string url)
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");

        var response = await organiser.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
