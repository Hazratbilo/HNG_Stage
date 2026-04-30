# Insighta Labs+ Stage 3 Implementation Plan

This plan outlines the architecture and execution strategy for turning the current Profile Intelligence System into a secure, multi-interface platform.

## Architecture Overview

We will have three main components within this workspace (acting as the 3 repositories for your submission):
1. **Backend**: ASP.NET Core API (`c:\Users\ALATOYE BILAL\source\repos\HNG_Stage_1\HNG_Stage_1`).
2. **CLI**: Node.js CLI tool (`c:\Users\ALATOYE BILAL\source\repos\HNG_Stage_1\insighta-cli` globally installable via `npm`).
3. **Web Portal**: React SPA web app (`c:\Users\ALATOYE BILAL\source\repos\HNG_Stage_1\insighta-web`).

## Proposed Changes

### 1. Backend Core Updates
#### Auth & DB
- Add `User` model (`id`, `github_id`, `username`, `email`, `avatar_url`, `role`, `is_active`, `last_login_at`, `created_at`), modify `ApplicationDbContext` to include `Users` table and mappings.
- Integrate Entity Framework migrations for the new table.
- Add GitHub OAuth integration with PKCE.
    - Endpoints: `GET /auth/github`, `GET /auth/github/callback`, `POST /auth/refresh`, `POST /auth/logout`.
    - Token generation using JWT for Access Tokens (3 mins) and opaque strings/record in DB for Refresh Tokens (5 mins expiration, invalidated upon use).
- Rate Limiting (`Microsoft.AspNetCore.RateLimiting` or custom middleware) configured for Auth routes (10/min) and others (60/min/user).
- Global request logging middleware (Method, Endpoint, Status, Response Time).

#### Profiles API Updates
- **API Versioning**: Add middleware or constraint to require `X-API-Version: 1` header.
- **RBAC (Role Based Access Control)**: Enforce authentication using JWT Bearer authentication globally and specific policies for `admin` and `analyst`. Only `admin` can create/delete. Default is `analyst`.
- **Pagination Structure**: Update `PagedProfilesResult` and dependent controller actions to match the new required shape containing `total_pages` and `links`.
- **CSV Export**: Add `GET /api/profiles/export?format=csv` that respects existing filters and streams a CSV.

### 2. CLI Tool (`insighta-cli`)
- Initialize a Node.js CLI project using `commander` or `yargs`.
- Uses a local express server to handle the GitHub OAuth callback on a random port.
- PKCE generation (`code_verifier` and `code_challenge`).
- Secure storage of tokens at `~/.insighta/credentials.json`.
- Automatic refresh token interception using `axios` interceptors.
- Format tabular output for `list` commands and loading spinners.

### 3. Web Portal (`insighta-web`)
- Initialize a React project (Next.js layout for full stack capabilities or Vite with backend handling cookies). I propose using Vite + React. 
- OAuth flow using a redirect to the backend. The backend will set HTTP-Only cookies with CSRF protection.
- Required pages: Login, Dashboard, Profiles list, Profile detail, Search, Account page.

## User Review Required

> [!IMPORTANT]
> **GitHub OAuth App Registration:** To implement login, you will need to register a GitHub OAuth app in your account settings and provide me with the `Client ID` and `Client Secret` to put into the backend `.env` or `appsettings.json`. Will you provide these? Wait until we start executing for you to insert them so they are not shared unnecessarily.
> **Web Framework:** I plan to use Node.js for the CLI and React for the Web Portal. Does that work for you?
> **Directory Structure:** I will create two new top-level directories side-by-side with the current `HNG_Stage_1` folder: `insighta-cli` and `insighta-web`. Is this correct for your "Three separate repos" expectation?

## Verification Plan

### Automated Tests
- Run `.NET` backend natively, test API behavior via API queries or CLI.

### Manual Verification
1. We will verify the OAuth login flow on the CLI via browser opening and callback.
2. We will check the JSON pagination structure outputs.
3. We will verify the `admin` vs `analyst` role constraints using manual DB role toggling to simulate behavior.
4. We will export CSV and verify its integrity.
