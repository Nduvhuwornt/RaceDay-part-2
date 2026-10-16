# RaceDay – API Endpoint Plan (Part 1, Section B)

Roles referenced below: **Organiser**, **Participant**, **Any** (any authenticated user), **None** (public, no login required).

## 1. Authentication

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/auth/register | Registers a new user as either an Organiser or a Participant. | None | `{ fullName, email, password, phoneNumber, role, organiserCode }` (`organiserCode` required only when role is Organiser) | 201 Created – user summary (no password) <br>400 Bad Request – validation failed <br>403 Forbidden – organiser code missing or invalid <br>409 Conflict – email already registered |
| POST | /api/auth/login | Authenticates a user and starts a session, storing the user's ID and role. | None | `{ email, password }` | 200 OK – `{ userId, fullName, role }` <br>401 Unauthorized – invalid credentials |
| POST | /api/auth/logout | Ends the current user's session. | Any | None | 200 OK – logout confirmed |

## 2. User Profile

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/users/me | Returns the profile of the currently logged-in user. | Any | None | 200 OK – user profile <br>401 Unauthorized – no active session |
| PUT | /api/users/me | Updates the logged-in user's own profile details. | Any | `{ fullName, phoneNumber, profilePictureUrl }` | 200 OK – updated profile <br>400 Bad Request – validation failed |

## 3. Events

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events | Lists events. Optional query filters: `type`, `search`, `upcomingOnly` (default true), `mine` (Organiser's own events). | Any | None | 200 OK – array of events |
| GET | /api/events/{id} | Returns full detail for a single event, including its categories. | Any | None | 200 OK – event detail <br>404 Not Found – event does not exist |
| POST | /api/events | Creates a new event owned by the logged-in Organiser. | Organiser | `{ name, description, eventDate, location, distanceKm, eventType }` | 201 Created – new event <br>400 Bad Request – validation failed |
| PUT | /api/events/{id} | Updates an event owned by the logged-in Organiser. | Organiser | `{ name, description, eventDate, location, distanceKm, eventType }` | 200 OK – updated event <br>403 Forbidden – not the owning Organiser <br>404 Not Found |
| DELETE | /api/events/{id} | Deletes an event owned by the logged-in Organiser. | Organiser | None | 204 No Content <br>403 Forbidden – not the owning Organiser <br>404 Not Found |

## 4. Categories

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events/{eventId}/categories | Lists all categories defined for a specific event. | Any | None | 200 OK – array of categories <br>404 Not Found – event does not exist |
| POST | /api/events/{eventId}/categories | Adds a new age or distance category to an event. | Organiser | `{ name, minAge, maxAge, distanceKm }` | 201 Created – new category <br>403 Forbidden – not the owning Organiser |
| PUT | /api/categories/{id} | Updates an existing category. | Organiser | `{ name, minAge, maxAge, distanceKm }` | 200 OK – updated category <br>403 Forbidden <br>404 Not Found |
| DELETE | /api/categories/{id} | Removes a category from an event. | Organiser | None | 204 No Content <br>403 Forbidden <br>404 Not Found <br>409 Conflict – participants already enrolled |

## 5. Event Enrolments

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/events/{eventId}/enrolments | Enrols the logged-in Participant into an event under a chosen category. | Participant | `{ categoryId }` | 201 Created – enrolment record (status Confirmed) <br>400 Bad Request – event already taken place <br>404 Not Found – event/category does not exist <br>409 Conflict – already enrolled |
| GET | /api/users/me/enrolments | Returns all events the logged-in Participant has enrolled in. | Participant | None | 200 OK – array of enrolments |
| GET | /api/events/{eventId}/enrolments | Returns all Participants enrolled in an event owned by the logged-in Organiser. | Organiser | None | 200 OK – array of enrolments <br>403 Forbidden – not the owning Organiser |

## 6. Results

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/enrolments/{enrolmentId}/result | Captures the finish time and position for a Participant's enrolment after the event. | Organiser | `{ finishTime, finishPosition }` | 201 Created – result record <br>400 Bad Request – event has not taken place yet <br>403 Forbidden – not the owning Organiser <br>404 Not Found – enrolment does not exist <br>409 Conflict – result already captured or position already taken |
| GET | /api/users/me/results | Returns the logged-in Participant's full personal race history. | Participant | None | 200 OK – array of results with event name, date, category, finish time, and position |
| GET | /api/events/{eventId}/results | Returns all captured results for an event owned by the logged-in Organiser. | Organiser | None | 200 OK – array of results <br>403 Forbidden – not the owning Organiser |

## Part 2 refinements (explained deviations from the first plan)

- **Organiser registration code** – registering as an Organiser requires a secret `organiserCode` configured on the server, so a Participant cannot simply choose the Organiser role.
- **Extra failure codes** – `400` when enrolling after an event or capturing a result before it; `409` when deleting a category that has enrolments or reusing a finishing position within an event.
- **Event list filters** – `type`, `search`, `upcomingOnly` and `mine` query parameters support the Part 3 browse and dashboard pages.
- **Enrolment status** – new enrolments are created as `Confirmed`.
