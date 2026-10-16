using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Tests;

public class EventTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public EventTests(RaceDayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Organiser_CanCreateEvent_Returns201()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", ApiHelpers.EventBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Test Fun Run", json.GetProperty("name").GetString());
        Assert.Equal("Run", json.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Organiser_CreateEvent_WithInvalidDistance_Returns400()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name = "Bad Event",
            eventDate = DateTime.UtcNow.AddDays(10),
            location = "Durban",
            distanceKm = -5,
            eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CreateEvent_WithInvalidEventType_Returns400()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/events", new
        {
            name = "Swim Event",
            eventDate = DateTime.UtcNow.AddDays(10),
            location = "Durban",
            distanceKm = 5,
            eventType = "Swim"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent_Returns200()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);

        var response = await organiser.PutAsJsonAsync($"/api/events/{eventId}", ApiHelpers.EventBody(name: "Renamed Run"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Renamed Run", json.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent_Returns204_ThenEventIsGone()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);

        var delete = await organiser.DeleteAsync($"/api/events/{eventId}");
        var get = await organiser.GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var (owner, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (other, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await ApiHelpers.CreateEventAsync(owner);

        var response = await other.PutAsJsonAsync($"/api/events/{eventId}", ApiHelpers.EventBody(name: "Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_Returns403()
    {
        var (owner, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (other, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await ApiHelpers.CreateEventAsync(owner);

        var response = await other.DeleteAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_NonexistentEvent_Returns404()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");

        var response = await organiser.PutAsJsonAsync("/api/events/999999", ApiHelpers.EventBody());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanViewEventListAndDetail_Returns200()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (participant, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);
        await ApiHelpers.CreateCategoryAsync(organiser, eventId);

        var list = await participant.GetAsync("/api/events");
        var detail = await participant.GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var json = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetProperty("categories").GetArrayLength());
    }

    [Fact]
    public async Task EventList_CanFilterByType()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        await organiser.PostAsJsonAsync("/api/events", new
        {
            name = "Filter Cycle Tour",
            eventDate = DateTime.UtcNow.AddDays(20),
            location = "Cape Town",
            distanceKm = 109,
            eventType = "Cycle"
        });

        var cycles = await organiser.GetFromJsonAsync<JsonElement>("/api/events?type=Cycle");

        Assert.True(cycles.GetArrayLength() >= 1);
        foreach (var item in cycles.EnumerateArray())
        {
            Assert.Equal("Cycle", item.GetProperty("eventType").GetString());
        }
    }

    [Fact]
    public async Task Organiser_CanAddAndListCategories()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);

        var create = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new { name = "Under 20", minAge = 10, maxAge = 19 });
        var list = await organiser.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/categories");

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.Equal(1, list.GetArrayLength());
    }

    [Fact]
    public async Task Organiser_AddCategory_WithMinAgeAboveMaxAge_Returns400()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new { name = "Broken", minAge = 50, maxAge = 20 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
