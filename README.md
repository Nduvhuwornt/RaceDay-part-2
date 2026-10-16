# RaceDay - PROG6212 Programming 2B PoE

## System Description
RaceDay is a web-based event management system for the South African road running, walking and cycling
community. Organisers create and manage events, categories and results. Participants browse events,
enter them by choosing a category, and track their personal results.

## Roles
- **Organiser** - creates, edits and deletes their own events; manages event categories; views all enrolments
  for their events; captures finish times and positions.
- **Participant** - registers, browses events, enrols in an event by selecting a category, views their own
  enrolments and personal results.

## Repository Layout
```
README.md
RaceDay.sln
docs/                      Part 1 - ERD, endpoint plan (PDF + MD), SQL script
script/                    Copy of the SQL script (RaceDay_Database.sql)
src/RaceDay.Api/           Part 2 - ASP.NET Core Web API (EF Core, Swagger, session auth)
tests/RaceDay.Tests/       Part 2 - xUnit integration tests
.github/workflows/         validate-docs.yml (Part 1) and ci.yml (build + test)
```

## Part 1 - Planning (see /docs)
`RaceDay_ERD.png/.pdf`, `API_Endpoint_Plan.pdf/.md`, `RaceDay_Database.sql`.
Run the SQL script in SSMS on a clean SQL Server instance to create `RaceDayDB` with seed data.

## Part 2 - RESTful API

### Requirements
- .NET 8 SDK
- SQL Server (LocalDB, SQL Express or full SQL Server)
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`

### Setup and run
1. Check the connection string in `src/RaceDay.Api/appsettings.json` (`ConnectionStrings:DefaultConnection`).
2. Create the first migration (Code-First) from the repository root:
   ```
   dotnet ef migrations add InitialCreate --project src/RaceDay.Api
   ```
3. Run the API (it applies migrations on start-up):
   ```
   dotnet run --project src/RaceDay.Api
   ```
4. Swagger opens at `http://localhost:5238/swagger` (set in `Properties/launchSettings.json`).

### Using Swagger
1. `POST /api/auth/register` - register a Participant, or an Organiser (needs the `organiserCode`
   from `appsettings.json`, key `Registration:OrganiserCode`).
2. `POST /api/auth/login` - the session cookie is stored by the browser.
3. Try the other endpoints. A `401` means not logged in; a `403` means the wrong role.

### Authentication and roles
- Passwords are hashed with PBKDF2 (SHA-256, random salt) and never stored in plain text.
- On login the server stores the user's ID and role in a server-side session; every protected endpoint checks it
  (`SessionAuthorize` attribute): `401` when logged out, `403` for the wrong role.
- Organisers can only change events, categories, enrolments and results that belong to their own events.
- Registering as an Organiser requires a secret organiser code, so users cannot pick the Organiser role freely.

### Unit tests
```
dotnet test RaceDay.sln
```
Tests boot the real API in memory with an isolated EF Core in-memory database and cover registration and login,
event management by Organisers, role rejection (401/403), enrolments and results.

### Differences from the Part 1 plan
Explained at the bottom of `docs/API_Endpoint_Plan.pdf`: organiser registration code, a few extra failure codes
(400/403/409), event list filters, and new enrolments being `Confirmed`. The code-first schema matches the ERD;
the SQL script's `DEFAULT GETDATE()` values are set in application code instead.

## CI/CD
- `ci.yml` restores, builds and runs all unit tests on every push.
- `validate-docs.yml` checks the `/docs` folder and required files exist.
- [ ] Add screenshot of the green build here.

## Video
- [ ] Add the unlisted YouTube link here (demo the running API in Swagger, explain code structure, endpoints,
  authentication and the unit tests; your own voice, no AI voiceover).

## AI Disclosure
- [ ] State briefly which parts were drafted with AI assistance (planning documents, API code scaffold, tests) and
  what you reviewed, changed and tested yourself.


Workflow
<img width="1920" height="1080" alt="image" src="https://github.com/user-attachments/assets/e24d7470-8a68-49b3-9932-fc54e45a67ad" />

