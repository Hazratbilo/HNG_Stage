# HNG Stage 1 API

`HNG Stage 1` is an ASP.NET Core Web API that creates and stores lightweight name-based profile records. When a new name is submitted, the service calls three public enrichment APIs:

- `Genderize` to predict gender
- `Agify` to estimate age
- `Nationalize` to infer the most likely country

The enriched result is saved to a local SQLite database and returned to the client. If the same name is submitted again, the existing profile is returned instead of creating a duplicate.

## Features

- Create a profile from a single `name`
- Reuse an existing profile for duplicate names
- Fetch one profile by ID
- List all saved profiles
- Filter profiles by `gender`, `country_id`, and `age_group`
- Delete a profile by ID
- Persist data with `Entity Framework Core` and `SQLite`
- Run locally with `.NET 8` or in Docker

## Tech Stack

- `ASP.NET Core 8`
- `Entity Framework Core`
- `SQLite`
- `UUIDNext`
- `Docker`

## Project Structure

```text
HNG_Stage_1/
|-- HNG_Stage_1.slnx
|-- README.md
`-- HNG_Stage_1/
    |-- Controllers/
    |-- Data/
    |-- Migrations/
    |-- Models/
    |-- Services/
    |-- Program.cs
    |-- Dockerfile
    `-- app.db
```

## How It Works

1. A client sends a `POST /api/profiles` request with a name.
2. The service checks whether that name already exists in the database.
3. If it does not exist, the app calls:
   - `https://api.genderize.io`
   - `https://api.agify.io`
   - `https://api.nationalize.io`
4. The app combines the results into a single profile object.
5. The record is stored in SQLite and returned to the client.

## Requirements

Before running the project locally, make sure you have:

- `.NET SDK 8.0`
- `Docker` (optional, for containerized runs)
- Internet access for the external enrichment APIs

## Local Setup

1. Clone the repository:

```bash
git clone <your-repository-url>
cd HNG_Stage_1
```

2. Move into the application folder:

```bash
cd HNG_Stage_1
```

3. Restore dependencies:

```bash
dotnet restore
```

4. Run the API:

```bash
dotnet run
```

By default, the development profiles are configured to run on:

- `http://localhost:5010`
- `https://localhost:7090`

## Database

The application uses SQLite with a local database file:

```text
HNG_Stage_1/app.db
```

Entity Framework migrations are applied automatically on startup, so the database schema is created or updated when the app launches.

## API Endpoints

Base route:

```text
/api/profiles
```

### Create Profile

`POST /api/profiles`

Request body:

```json
{
  "name": "john"
}
```

Possible responses:

- `201 Created` when a new profile is saved
- `200 OK` when the profile already exists
- `400 Bad Request` for missing or empty name
- `502 Bad Gateway` when one of the external APIs fails

Example success response:

```json
{
  "status": "success",
  "data": {
    "id": "01963c7e-b1d7-7c2e-a1b2-123456789abc",
    "name": "john",
    "gender": "male",
    "gender_probability": 0.99,
    "sample_size": 12345,
    "age": 34,
    "age_group": "adult",
    "country_id": "US",
    "country_probability": 0.12,
    "created_at": "2026-04-17T10:00:00Z"
  }
}
```

### Get Profile By ID

`GET /api/profiles/{id}`

Example:

```bash
curl http://localhost:5010/api/profiles/<profile-id>
```

Responses:

- `200 OK` when found
- `404 Not Found` when the ID does not exist

### List Profiles

`GET /api/profiles`

Optional query parameters:

- `gender`
- `country_id`
- `age_group`

Examples:

```bash
curl http://localhost:5010/api/profiles
curl "http://localhost:5010/api/profiles?gender=male"
curl "http://localhost:5010/api/profiles?country_id=US&age_group=adult"
```

Example response:

```json
{
  "status": "success",
  "count": 2,
  "data": [
    {
      "id": "01963c7e-b1d7-7c2e-a1b2-123456789abc",
      "name": "john",
      "gender": "male",
      "age": 34,
      "age_group": "adult",
      "country_id": "US"
    }
  ]
}
```

### Delete Profile

`DELETE /api/profiles/{id}`

Responses:

- `204 No Content` when deleted successfully
- `404 Not Found` when the ID does not exist

## Running With Docker

From the application directory:

```bash
cd HNG_Stage_1
docker build -t hng-stage-1-api .
docker run -p 8080:8080 hng-stage-1-api
```

The API will be available at:

```text
http://localhost:8080
```

## Error Handling

The API returns structured JSON error responses in this format:

```json
{
  "status": "error",
  "message": "Description of the error"
}
```

Examples include:

- missing request data
- invalid external API responses
- internal server errors

## Notes

- Profile names are normalized to lowercase before storage.
- A unique index is configured on the `Name` field.
- CORS is currently configured to allow any origin, method, and header.
- HTTPS redirection is enabled in the application pipeline.
- Swagger/OpenAPI is not currently configured in this project.

## Quick Test With cURL

Create a profile:

```bash
curl -X POST http://localhost:5010/api/profiles \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"john\"}"
```

List profiles:

```bash
curl http://localhost:5010/api/profiles
```

## License

This project is available for learning, assessment, and further extension. Add a license file if you plan to distribute it publicly.
