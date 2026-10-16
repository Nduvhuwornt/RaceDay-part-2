using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Tests;

public class AuthTests : IClassFixture<RaceDayApiFactory>
{
    private readonly RaceDayApiFactory _factory;

    public AuthTests(RaceDayApiFactory factory) => _factory = factory;

    private static object ParticipantBody(string email) => new
    {
        fullName = "Thandiwe Nkosi",
        email,
        password = ApiHelpers.Password,
        phoneNumber = "0721112222",
        role = "Participant"
    };

    [Fact]
    public async Task Register_Participant_Returns201()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", ParticipantBody($"{Guid.NewGuid():N}@test.co.za"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Participant", json.GetProperty("role").GetString());
        Assert.False(json.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Register_Organiser_WithValidCode_Returns201()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Naledi Khumalo",
            email = $"{Guid.NewGuid():N}@test.co.za",
            password = ApiHelpers.Password,
            role = "Organiser",
            organiserCode = RaceDayApiFactory.OrganiserCode
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithoutValidCode_Returns403()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Sneaky Participant",
            email = $"{Guid.NewGuid():N}@test.co.za",
            password = ApiHelpers.Password,
            role = "Organiser",
            organiserCode = "WRONG-CODE"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@test.co.za";

        var first = await client.PostAsJsonAsync("/api/auth/register", ParticipantBody(email));
        var second = await client.PostAsJsonAsync("/api/auth/register", ParticipantBody(email));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", ParticipantBody("not-an-email"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_UnknownRole_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Test User",
            email = $"{Guid.NewGuid():N}@test.co.za",
            password = ApiHelpers.Password,
            role = "Admin"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotPlainText()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@test.co.za";
        await client.PostAsJsonAsync("/api/auth/register", ParticipantBody(email));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var user = db.Users.Single(u => u.Email == email);

        Assert.NotEqual(ApiHelpers.Password, user.PasswordHash);
        Assert.StartsWith("v1.", user.PasswordHash);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithRole()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@test.co.za";
        await client.PostAsJsonAsync("/api/auth/register", ParticipantBody(email));

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiHelpers.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Participant", json.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        var email = $"{Guid.NewGuid():N}@test.co.za";
        await client.PostAsJsonAsync("/api/auth/register", ParticipantBody(email));

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUser_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "nobody@test.co.za", password = ApiHelpers.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Session_AfterLogin_AllowsProfileAccess()
    {
        var (client, email) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, json.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Profile_WithoutLogin_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession_ThenProfileReturns401()
    {
        var (client, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");

        var logout = await client.PostAsync("/api/auth/logout", null);
        var after = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_ChangesOwnDetails()
    {
        var (client, _) = await ApiHelpers.CreateLoggedInClientAsync(_factory, "Participant");

        var update = await client.PutAsJsonAsync("/api/users/me", new { fullName = "Updated Name", phoneNumber = "0831112222" });
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Updated Name", profile.GetProperty("fullName").GetString());
    }
}
