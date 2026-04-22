# HNG Stage 2 API

`HNG Stage 2` is an ASP.NET Core Web API for demographic profile storage and intelligence queries. It supports seeded profile data, exact filtering, sorting, pagination, and a rule-based natural language search endpoint.

## Features

- Seed `2026` profiles from JSON on startup without creating duplicates
- Query all profiles with combinable filters
- Sort results by `age`, `created_at`, or `gender_probability`
- Paginate responses with `page` and `limit`
- Parse plain-English search queries without AI or LLMs
- Return structured JSON error responses
- Store timestamps in UTC ISO 8601 format
- Allow cross-origin access with `Access-Control-Allow-Origin: *`

## Tech Stack

- `ASP.NET Core 8`
- `Entity Framework Core`
- `SQLite`
- `UUIDNext`

## Database Schema

The `profiles` table follows this structure:

| Field | Type | Notes |
|---|---|---|
| `id` | UUID v7 | Primary key |
| `name` | VARCHAR + UNIQUE | Person's full name |
| `gender` | VARCHAR | `male` or `female` |
| `gender_probability` | FLOAT | Confidence score |
| `age` | INT | Exact age |
| `age_group` | VARCHAR | `child`, `teenager`, `adult`, `senior` |
| `country_id` | VARCHAR(2) | ISO code such as `NG`, `BJ` |
| `country_name` | VARCHAR | Full country name |
| `country_probability` | FLOAT | Confidence score |
| `created_at` | TIMESTAMP | Auto-generated UTC timestamp |

## Project Structure

```text
HNG_Stage/
|-- README.md
`-- HNG_Stage_1/
    |-- Controllers/
    |-- Data/
    |-- Models/
    |-- SeedData/
    |-- Services/
    |-- Program.cs
    |-- HNG_Stage_1.csproj
    `-- app.db
```

## Setup

1. Clone the repository.
2. Move into the project root.
3. Restore packages.
4. Run the API.

```bash
git clone <your-repository-url>
cd HNG_Stage
dotnet restore HNG_Stage_1/HNG_Stage_1.csproj
dotnet run --project HNG_Stage_1/HNG_Stage_1.csproj
```

Default development URLs:

- `http://localhost:5010`
- `https://localhost:7090`

## Seeding

The application seeds the database on startup from:

```text
HNG_Stage_1/SeedData/profiles.json
```

Supported seed file shapes:

- a raw array of profiles
- an object in the form `{ "profiles": [...] }`

The seeder normalizes names to lowercase, uses UUID v7 IDs for inserted rows, and skips any profile whose normalized name already exists. That makes repeated startup seeding idempotent.

## API Endpoints

Base route:

```text
/api/profiles
```

### GET /api/profiles

Returns profiles with filtering, sorting, and pagination.

Supported filters:

- `gender`
- `age_group`
- `country_id`
- `min_age`
- `max_age`
- `min_gender_probability`
- `min_country_probability`

Sorting:

- `sort_by`: `age` | `created_at` | `gender_probability`
- `order`: `asc` | `desc`

Pagination:

- `page`: default `1`
- `limit`: default `10`, max `50`

Example:

```text
/api/profiles?gender=male&country_id=NG&min_age=25&sort_by=age&order=desc&page=1&limit=10
```

Example success response:

```json
{
  "status": "success",
  "page": 1,
  "limit": 10,
  "total": 43,
  "data": [
    {
      "id": "019db5f7-0252-77a7-aac0-20174e0e6efd",
      "name": "tunde barro",
      "gender": "male",
      "gender_probability": 0.79,
      "age": 84,
      "age_group": "senior",
      "country_id": "NG",
      "country_name": "Nigeria",
      "country_probability": 0.47,
      "created_at": "2026-04-22T16:12:37.3301375Z"
    }
  ]
}
```

### GET /api/profiles/search

Rule-based natural language query endpoint.

Query parameters:

- `q`: the natural language search string
- `page`: default `1`
- `limit`: default `10`, max `50`

Example:

```text
/api/profiles/search?q=young males from nigeria&page=1&limit=5
```

If the query cannot be interpreted, the API returns:

```json
{
  "status": "error",
  "message": "Unable to interpret query"
}
```

### Existing CRUD Endpoints

The API also keeps the Stage 1 endpoints:

- `POST /api/profiles`
- `GET /api/profiles/{id}`
- `DELETE /api/profiles/{id}`

## Natural Language Parsing Approach

The parser is fully rule-based. It does not use AI, LLMs, embeddings, or external NLP services.

Implementation lives in `HNG_Stage_1/Services/ProfileSearchQueryParser.cs`.

### Supported Keywords and Mappings

Gender keywords:

- `male`, `males`, `man`, `men`, `boy`, `boys` -> `gender=male`
- `female`, `females`, `woman`, `women`, `girl`, `girls` -> `gender=female`
- if both male and female words appear, no gender filter is applied

Age descriptors:

- `young` -> `min_age=16` and `max_age=24`
- `child` -> `age_group=child`
- `teenager` or `teenagers` -> `age_group=teenager`
- `adult` or `adults` -> `age_group=adult`
- `senior` or `seniors` -> `age_group=senior`

Age comparison phrases:

- `above 30`, `over 30`, `older than 30` -> `min_age=30`
- `below 18`, `under 18`, `younger than 18` -> `max_age=18`
- `between 20 and 30` -> `min_age=20` and `max_age=30`

Country phrases:

- `from angola` -> `country_id=AO`
- `from nigeria` -> `country_id=NG`
- standalone country names such as `kenya`, `angola`, `nigeria`, `benin` are also matched
- the parser uses a country lookup table built from `RegionInfo`, with a few explicit overrides for common examples

### Parsing Flow

1. Normalize the input by trimming, lowercasing, and collapsing repeated spaces.
2. Detect gender keywords.
3. Detect the special `young` range.
4. Detect stored age groups.
5. Detect age comparison patterns with regular expressions.
6. Detect country references using `from <country>` or known country names.
7. Merge all recognized rules into a single `ProfileQueryParameters` object.
8. Reject contradictory age ranges such as `young above 30`.

### Example Mappings

- `young males` -> `gender=male`, `min_age=16`, `max_age=24`
- `females above 30` -> `gender=female`, `min_age=30`
- `people from angola` -> `country_id=AO`
- `adult males from kenya` -> `gender=male`, `age_group=adult`, `country_id=KE`
- `male and female teenagers above 17` -> `age_group=teenager`, `min_age=17`

## Parser Limitations

- It does not support negation such as `not male` or `excluding nigeria`.
- It does not support free-form boolean grouping with parentheses.
- It does not rank results semantically; it only converts recognized words into filters.
- It does not correct spelling mistakes or fuzzy-match misspelled country names.
- It applies at most one country filter.
- It does not understand unsupported phrases such as `middle aged`, `youngish`, or `top confidence`.
- `young` is only a parsing shortcut for ages `16-24`; it is not stored as an `age_group`.
- If a query contains no recognized rule, the API returns `Unable to interpret query`.

## Validation and Error Handling

All error responses follow this structure:

```json
{
  "status": "error",
  "message": "<error message>"
}
```

Status codes:

- `400 Bad Request` for missing or empty required parameters
- `400 Bad Request` for uninterpretable natural-language queries
- `422 Unprocessable Entity` for invalid query parameter values or invalid parameter types
- `404 Not Found` when a profile does not exist
- `500 Internal Server Error` for unexpected server failures
- `502 Bad Gateway` for external enrichment API failures

Examples:

```json
{
  "status": "error",
  "message": "Invalid query parameters"
}
```

```json
{
  "status": "error",
  "message": "Missing or empty parameter"
}
```

## Performance Notes

- Filtering, sorting, and pagination are translated into database queries through EF Core.
- The service applies `Where`, `OrderBy`, `Skip`, and `Take` before materializing results.
- The query path uses `AsNoTracking()` for read-only operations.
- Indexes are created on `name`, `age`, `created_at`, and the composite `(gender, age_group, country_id)` columns.
- The service counts filtered rows first, then fetches only the requested page.

## Quick Checks

Run the API:

```bash
dotnet run --project HNG_Stage_1/HNG_Stage_1.csproj
```

List profiles:

```bash
curl "http://localhost:5010/api/profiles?page=1&limit=5"
```

Combined filter:

```bash
curl "http://localhost:5010/api/profiles?gender=male&country_id=NG&min_age=25&sort_by=age&order=desc&page=1&limit=10"
```

Natural language search:

```bash
curl "http://localhost:5010/api/profiles/search?q=young%20males%20from%20nigeria&page=1&limit=5"
```

Invalid parameter example:

```bash
curl "http://localhost:5010/api/profiles?limit=99"
```
