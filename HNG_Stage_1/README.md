# Insighta Backend

ASP.NET Core backend for Insighta Labs+.

## Features

- Profile creation, filtering, sorting, pagination, and natural-language search
- GitHub OAuth with PKCE for browser and CLI clients
- JWT access tokens and rotating refresh tokens
- Role-based access control for `admin` and `analyst`
- CSV profile export
- Request logging and fixed-window rate limiting
- Cookie auth support for the web portal with CSRF protection

## Required configuration

Use environment variables or a secure secret source:

```text
Jwt__Key=<strong-random-secret>
Jwt__Issuer=InsightaLabs
Jwt__Audience=InsightaLabsUsers
GitHub__ClientId=<github-oauth-client-id>
GitHub__ClientSecret=<github-oauth-client-secret>
Frontend__BaseUrl=http://localhost:5173
Cors__AllowedOrigins__0=http://localhost:5173
BootstrapAdmin__GitHubIds__0=<optional-github-id>
```

PowerShell template:

```powershell
. .\env.example.ps1
```

## Run

```bash
dotnet restore
dotnet run
```
