# Insighta Labs+ Stage 3

Insighta Labs+ is a profile intelligence platform built on top of the Stage 2 profile system. It keeps the existing filtering, sorting, pagination, and natural-language search behavior, then adds secure authentication, session handling, role-based access, a browser portal, and a globally installable CLI.

## Repositories

This workspace currently contains three projects:

- `HNG_Stage_1/` - ASP.NET Core backend API
- `insighta-cli/` - Node.js CLI
- `insighta-web/` - React web portal

For submission, publish each project as its own repository.

## System Architecture

The backend is the single source of truth. Both clients talk to the same API and share the same authorization rules.

- The backend stores profiles, users, and refresh tokens in SQLite.
- The backend enriches new profiles with `Genderize`, `Agify`, and `Nationalize`.
- The CLI authenticates with GitHub OAuth + PKCE, stores tokens in `~/.insighta/credentials.json`, and sends bearer tokens on API requests.
- The web portal authenticates with GitHub OAuth + PKCE, stores access and refresh tokens in HTTP-only cookies, and uses a CSRF token cookie + header for unsafe requests.

## Authentication Flow

### Browser flow

1. The user clicks `Continue with GitHub` in the portal.
2. The backend generates a PKCE verifier and OAuth state, stores both in HTTP-only cookies, and redirects to GitHub.
3. GitHub redirects back to the backend callback.
4. The backend validates the OAuth state, exchanges the code with GitHub, creates or updates the local user, and issues:
   - a short-lived access token in an HTTP-only cookie
   - a short-lived refresh token in an HTTP-only cookie
   - a CSRF token cookie for unsafe browser requests
5. The backend redirects the user back to the frontend dashboard.

### CLI flow

1. The CLI starts a temporary localhost callback server.
2. The CLI generates `state`, `code_verifier`, and `code_challenge`.
3. The CLI fetches the GitHub client id from the backend, opens the GitHub consent page, and uses the localhost callback as the redirect URI.
4. After GitHub redirects to the local callback, the CLI validates the returned state locally.
5. The CLI sends the GitHub code and PKCE verifier to the backend callback endpoint.
6. The backend exchanges the code, creates or updates the local user, and returns access and refresh tokens in JSON.

## Token Handling Approach

- Access tokens are JWTs signed by the backend and expire after `3 minutes`.
- Refresh tokens are random opaque strings stored server-side and expire after `5 minutes`.
- Refresh tokens are single-use. Once a refresh happens, the old token is marked as used and a new refresh token is issued.
- CLI credentials are stored in `~/.insighta/credentials.json`.
- Browser credentials are stored in HTTP-only cookies, so JavaScript cannot read the access or refresh tokens directly.
- Browser refresh and logout requests require a CSRF token header that matches the CSRF cookie.

## Role Enforcement Logic

Two roles are supported:

- `analyst`
- `admin`

Rules:

- All `/api/profiles` endpoints require authentication.
- `admin` is required for `POST /api/profiles` and `DELETE /api/profiles/{id}`.
- `analyst` and `admin` can read profiles, search, and export CSV.
- New users default to `analyst` unless their GitHub id, username, or email matches the configured bootstrap admin lists.

Configure bootstrap admins with environment-backed settings:

- `BootstrapAdmin__GitHubIds__0`
- `BootstrapAdmin__Usernames__0`
- `BootstrapAdmin__Emails__0`

## API Versioning

Protected profile endpoints require:

```text
X-API-Version: 1
```

The response pagination shape is:

```json
{
  "status": "success",
  "page": 1,
  "limit": 10,
  "total": 42,
  "total_pages": 5,
  "links": {
    "self": "/api/profiles?page=1&limit=10",
    "next": "/api/profiles?page=2&limit=10",
    "prev": null
  },
  "data": []
}
```

## Natural Language Parsing Approach

The natural-language search feature from Stage 2 remains in place. The backend parser converts user-friendly phrases such as:

```text
young females in nigeria
```

into the structured filters already supported by the query layer, including gender, age group, and country. If the parser cannot confidently map a phrase to known filters, the backend returns a structured `400` error.

## Security Controls

- GitHub OAuth with PKCE for both clients
- OAuth state validation for browser sign-in
- JWT access tokens with short expiry
- Server-stored rotating refresh tokens
- Role-based authorization policies
- HTTP-only cookies for browser auth
- CSRF protection for cookie-authenticated unsafe requests
- Fixed-window rate limiting for auth and API traffic
- Request logging middleware
- CORS restricted to configured frontend origins

## Backend Setup

### Required configuration

Set these values with environment variables or a secure secret store:

```text
Jwt__Key=<strong-random-32-byte-minimum-secret>
Jwt__Issuer=InsightaLabs
Jwt__Audience=InsightaLabsUsers
GitHub__ClientId=<github-oauth-client-id>
GitHub__ClientSecret=<github-oauth-client-secret>
Frontend__BaseUrl=http://localhost:5173
Cors__AllowedOrigins__0=http://localhost:5173
BootstrapAdmin__GitHubIds__0=<optional-github-id>
```

For local PowerShell setup, a starter template is available at [HNG_Stage_1/env.example.ps1](/C:/Users/ALATOYE%20BILAL/source/repos/HNG_Stage_1/HNG_Stage_1/env.example.ps1).

### Run locally

```bash
cd HNG_Stage_1
dotnet restore
dotnet run
```

## CLI Usage

### Install locally for global usage

```bash
cd insighta-cli
npm install
npm link
```

### Configure backend base URL

```bash
set INSIGHTA_API_URL=https://localhost:7198
```

### Commands

```bash
insighta login
insighta whoami
insighta profiles list --page 1 --limit 10
insighta profiles search "young males from nigeria"
insighta profiles get <profile-id>
insighta profiles create --name john
insighta profiles export --format csv
insighta logout
```

## Web Portal Usage

```bash
cd insighta-web
npm install
npm run dev
```

Optional environment variable:

```text
VITE_API_BASE_URL=https://localhost:7198
```

You can start from [insighta-web/.env.example](/C:/Users/ALATOYE%20BILAL/source/repos/HNG_Stage_1/insighta-web/.env.example).

## CSV Export

CSV export is available through:

- `GET /api/profiles/export?format=csv` for authenticated users
- `insighta profiles export --format csv` in the CLI
- the `Export CSV` action in the web portal

## Notes

- The backend currently uses SQLite for local development.
- Refresh token rotation is enforced server-side.
- The workspace should not commit production secrets, generated databases, or `node_modules`.
