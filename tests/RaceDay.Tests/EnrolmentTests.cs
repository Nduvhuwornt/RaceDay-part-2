using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Tests;

public class EnrolmentTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public EnrolmentTests(RaceDayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Participant_CanEnrol_Returns201_AndEnrolmentIsRecorded()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (participant, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiHelpers.CreateCategoryAsync(organiser, eventId);

        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var enrolmentId = created.GetProperty("enrolmentId").GetInt32();

        // Recorded in the participant's own list...
        var mine = await participant.GetFromJsonAsync<JsonElement>("/api/users/me/enrolments");
        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal(eventId, mine[0].GetProperty("eventId").GetInt32());
        Assert.Equal(categoryId, mine[0].GetProperty("categoryId").GetInt32());
        Assert.Equal("Confirmed", mine[0].GetProperty("status").GetString());

        // ...and in the database, linking participant, event and category.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var row = db.EventEnrolments.Single(x => x.EnrolmentId == enrolmentId);
        Assert.Equal(eventId, row.EventId);
        Assert.Equal(categoryId, row.CategoryId);
    }

    [Fact]
    public async Task Participant_EnrolTwiceInSameEvent_Returns409()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (participant, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiHelpers.CreateCategoryAsync(organiser, eventId);
        await ApiHelpers.EnrolAsync(participant, eventId, categoryId);

        var second = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_NonexistentEvent_Returns404()
    {
        var (participant, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");

        var response = await participant.PostAsJsonAsync("/api/events/999999/enrolments", new { categoryId = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_WithCategoryFromAnotherEvent_Returns404()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (participant, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");
        var eventA = await ApiHelpers.CreateEventAsync(organiser);
        var eventB = await ApiHelpers.CreateEventAsync(organiser);
        var categoryOfB = await ApiHelpers.CreateCategoryAsync(organiser, eventB);

        var response = await participant.PostAsJsonAsync($"/api/events/{eventA}/enrolments", new { categoryId = categoryOfB });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InEventThatAlreadyHappened_Returns400()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (participant, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");
        var eventId = await ApiHelpers.CreateEventAsync(organiser, DateTime.UtcNow.AddDays(-5));
        var categoryId = await ApiHelpers.CreateCategoryAsync(organiser, eventId);

        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewEnrolmentsForOwnEvent()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (participant, participantEmail) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiHelpers.CreateCategoryAsync(organiser, eventId);
        await ApiHelpers.EnrolAsync(participant, eventId, categoryId);

        var response = await organiser.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(participantEmail, list[0].GetProperty("participantEmail").GetString());
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsForAnotherOrganisersEvent_Returns403()
    {
        var (owner, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (other, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await ApiHelpers.CreateEventAsync(owner);

        var response = await other.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteCategoryWithEnrolments_Returns409()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (participant, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiHelpers.CreateCategoryAsync(organiser, eventId);
        await ApiHelpers.EnrolAsync(participant, eventId, categoryId);

        var response = await organiser.DeleteAsync($"/api/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
