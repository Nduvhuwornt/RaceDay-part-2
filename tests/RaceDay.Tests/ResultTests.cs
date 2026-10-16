using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Tests;

public class ResultTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public ResultTests(RaceDayApiFactory factory) => _factory = factory;

    /// <summary>Creates a future event, enrols a participant, then moves the event into the past so results can be captured.</summary>
    private async Task<(HttpClient Organiser, HttpClient Participant, int EventId, int EnrolmentId)> SetUpFinishedEventAsync()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (participant, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiHelpers.CreateCategoryAsync(organiser, eventId);
        var enrolmentId = await ApiHelpers.EnrolAsync(participant, eventId, categoryId);

        var moved = await organiser.PutAsJsonAsync($"/api/events/{eventId}", ApiHelpers.EventBody(DateTime.UtcNow.AddDays(-1)));
        moved.EnsureSuccessStatusCode();

        return (organiser, participant, eventId, enrolmentId);
    }

    [Fact]
    public async Task Organiser_CanCaptureResult_AndParticipantCanViewIt()
    {
        var (organiser, participant, _, enrolmentId) = await SetUpFinishedEventAsync();

        var capture = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", new { finishTime = "00:52:14", finishPosition = 47 });
        var mine = await participant.GetFromJsonAsync<JsonElement>("/api/users/me/results");

        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);
        Assert.Equal(1, mine.GetArrayLength());
        Assert.Equal(47, mine[0].GetProperty("finishPosition").GetInt32());
        Assert.Equal("00:52:14", mine[0].GetProperty("finishTime").GetString());
        Assert.Equal(1, mine[0].GetProperty("totalFinishers").GetInt32());
    }

    [Fact]
    public async Task Organiser_CanViewAllResultsForOwnEvent()
    {
        var (organiser, _, eventId, enrolmentId) = await SetUpFinishedEventAsync();
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", new { finishTime = "01:10:00", finishPosition = 3 });

        var results = await organiser.GetFromJsonAsync<JsonElement>($"/api/events/{eventId}/results");

        Assert.Equal(1, results.GetArrayLength());
    }

    [Fact]
    public async Task CaptureResult_Twice_Returns409()
    {
        var (organiser, _, _, enrolmentId) = await SetUpFinishedEventAsync();
        await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", new { finishTime = "00:50:00", finishPosition = 1 });

        var second = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", new { finishTime = "00:51:00", finishPosition = 2 });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ForEventThatHasNotHappened_Returns400()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var (participant, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");
        var eventId = await ApiHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiHelpers.CreateCategoryAsync(organiser, eventId);
        var enrolmentId = await ApiHelpers.EnrolAsync(participant, eventId, categoryId);

        var response = await organiser.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", new { finishTime = "00:50:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotCaptureResult_Returns403()
    {
        var (_, _, _, enrolmentId) = await SetUpFinishedEventAsync();
        var (other, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");

        var response = await other.PostAsJsonAsync($"/api/enrolments/{enrolmentId}/result", new { finishTime = "00:50:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ForUnknownEnrolment_Returns404()
    {
        var (organiser, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Organiser");

        var response = await organiser.PostAsJsonAsync("/api/enrolments/999999/result", new { finishTime = "00:50:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
