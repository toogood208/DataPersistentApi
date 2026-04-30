# Insighta Backend

`Insighta Backend` is the Stage 3 backend repository for Insighta Labs+. It is the shared API and auth service consumed by the CLI and web portal.

This backend is responsible for:

- GitHub OAuth login
- CLI-friendly PKCE login initiation
- user persistence and role assignment
- JWT access token issuance
- refresh token rotation and revocation
- cookie-backed browser sessions
- CSRF protection for cookie-authenticated write operations
- protected profile APIs
- filtering, sorting, pagination, and natural-language search
- CSV export
- rate limiting and request logging

## System Architecture

The full Stage 3 platform is split into three repositories:

- `insighta-backend`: this repository, the source of truth for auth, users, roles, and profile data access
- `insighta-cli`: a separate command-line client that calls this backend
- `insighta-web`: a separate browser client that calls this backend

The backend is the shared contract for both clients. Data, auth rules, role checks, pagination, search behavior, and export behavior are centralized here so all interfaces stay consistent.

## Tech Stack

- .NET `10.0`
- ASP.NET Core minimal APIs
- Entity Framework Core
- SQL Server with EF InMemory fallback
- JWT bearer authentication
- GitHub OAuth

## Configuration

The app reads configuration from:

1. `appsettings.json`
2. environment variables
3. .NET user-secrets in Development

Tracked config intentionally keeps secret values blank. Real credentials should be supplied through user-secrets or environment variables.

### Connection string

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=ProfilesDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

If `DefaultConnection` is empty, the app falls back to an in-memory database.

### GitHub OAuth settings

```json
"GitHub": {
  "ClientId": "",
  "ClientSecret": "",
  "CallbackUrl": "",
  "Scope": "read:user user:email",
  "AuthorizeUrl": "https://github.com/login/oauth/authorize",
  "TokenUrl": "https://github.com/login/oauth/access_token",
  "UserUrl": "https://api.github.com/user",
  "EmailsUrl": "https://api.github.com/user/emails"
}
```

Notes:

- `ClientId` and `ClientSecret` are required for live GitHub OAuth.
- `CallbackUrl` is optional. If omitted, the backend uses `/auth/github/callback` on the current host.
- CLI OAuth must supply a `client_redirect_uri` and PKCE challenge parameters.

### JWT settings

```json
"Auth": {
  "Issuer": "InsightaLabs",
  "Audience": "InsightaClients",
  "SigningKey": "",
  "AccessTokenMinutes": 3,
  "RefreshTokenMinutes": 5
}
```

Notes:

- access tokens expire after `3` minutes
- refresh tokens expire after `5` minutes
- refresh tokens are stored hashed in the database
- `Auth:SigningKey` is required; the backend will not start without it

### CORS settings

```json
"Cors": {
  "AllowedOrigins": []
}
```

Notes:

- when `AllowedOrigins` is empty, the backend allows any origin without credentials
- when one or more origins are configured, the backend enables credentialed CORS for those origins
- a separate web portal should set its real frontend origin here

### Bootstrap admin settings

```json
"RoleBootstrap": {
  "AdminUsernames": [],
  "AdminEmails": []
}
```

Notes:

- matching GitHub usernames or emails are assigned `admin` during login
- all other new users default to `analyst`
- this is the intended way to obtain an admin login for local testing without manual database edits

Example local setup with user-secrets:

```powershell
dotnet user-secrets init
dotnet user-secrets set "GitHub:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "GitHub:ClientSecret" "YOUR_CLIENT_SECRET"
dotnet user-secrets set "GitHub:CallbackUrl" "https://your-public-url/auth/github/callback"
dotnet user-secrets set "Auth:SigningKey" "YOUR_SIGNING_KEY"
dotnet user-secrets set "RoleBootstrap:AdminUsernames:0" "your-github-username"
```

## Running Locally

Restore and run:

```powershell
dotnet restore
dotnet run
```

Apply migrations:

```powershell
dotnet ef database update
```

Seed profiles locally:

```powershell
$env:SEED = "true"
dotnet run
```

Seeding notes:

- the seed loader accepts either a raw JSON array or an object with a top-level `profiles` array
- seeding is idempotent by normalized profile name

## Authentication Flow

### Browser login

Start GitHub OAuth:

`GET /auth/github`

Optional query parameters:

- `client_redirect_uri`
- `state`

If successful, the backend redirects to GitHub.

After GitHub callback, the backend:

1. exchanges the authorization code
2. fetches the GitHub profile and email
3. creates or updates the local user
4. assigns `admin` or `analyst`
5. issues an access token and refresh token
6. stores them in HTTP-only cookies for browser sessions
7. sets a CSRF cookie for state-changing requests

Browser session cookies:

- `insighta_access_token` as HTTP-only
- `insighta_refresh_token` as HTTP-only
- `insighta_csrf` as a readable CSRF token cookie

If `client_redirect_uri` was provided, the backend redirects back with a success or error fragment. Tokens are not exposed in the fragment.

### CLI / PKCE login

Start CLI login:

`GET /auth/github?mode=cli&code_challenge=...&code_challenge_method=S256`

Optional query parameters:

- `client_redirect_uri`
- `state`

The backend returns JSON containing:

- `data.authorize_url`
- `data.state`

CLI requirements:

- provide `client_redirect_uri`
- provide `code_challenge`
- provide `code_challenge_method=S256`
- send the returned protected `state` back to the callback exchange

Complete the flow with:

`GET /auth/github/callback?code=...&state=...&code_verifier=...`

Successful CLI-style callback response:

```json
{
  "status": "success",
  "access_token": "...",
  "refresh_token": "...",
  "token_type": "Bearer",
  "expires_in": 180,
  "refresh_expires_in": 300,
  "user": {
    "id": "uuid",
    "github_id": "123456",
    "username": "octocat",
    "email": "octocat@example.com",
    "avatar_url": "https://...",
    "role": "analyst",
    "is_active": true,
    "last_login_at": "timestamp",
    "created_at": "timestamp"
  }
}
```

### Refresh tokens

Refresh:

`POST /auth/refresh`

```json
{
  "refresh_token": "..."
}
```

Token-style success response:

```json
{
  "status": "success",
  "access_token": "...",
  "refresh_token": "..."
}
```

Cookie-style refresh:

- if the refresh token is supplied through the session cookie, the backend rotates cookies and also returns the rotated token pair in the response body
- cookie-based refresh requires `X-CSRF-Token`

Logout:

`POST /auth/logout`

```json
{
  "refresh_token": "..."
}
```

Token lifecycle rules:

- refresh tokens are hashed before storage
- refresh uses rotation
- the old refresh token is revoked immediately when exchanged
- logout revokes the provided refresh token

## User Model And Role Enforcement

Users are stored with:

- `id`
- `github_id`
- `username`
- `email`
- `avatar_url`
- `role`
- `is_active`
- `last_login_at`
- `created_at`

Roles:

- `admin`
- `analyst`

Role rules:

- `admin` can create and delete profiles, and can read/query everything
- `analyst` is read-only and can list, export, read individual profiles, and use natural-language search

Active-state enforcement:

- authenticated users must exist in the database
- inactive users are denied with `403`

### Current user endpoint

`GET /api/users/me`

This endpoint returns the authenticated user profile and is useful for CLI and web clients after login.

## API Requirements

### Authentication

All `/api/*` endpoints require authentication.

Clients can authenticate with either:

- `Authorization: Bearer <access-token>`
- browser session cookies issued by the backend

### API version header

All `/api/profiles*` endpoints require:

```http
X-API-Version: 1
```

Requests without that exact header return:

```json
{
  "status": "error",
  "message": "API version header required"
}
```

## Profile Endpoints

### Create profile

`POST /api/profiles`

Admin only.

Request body:

```json
{
  "name": "Ada"
}
```

Behavior:

- calls the Stage 1 external data providers
- transforms the result
- stores the saved profile

### Get profile by id

`GET /api/profiles/{id}`

Admin and analyst.

### List profiles

`GET /api/profiles`

Admin and analyst.

Supported query parameters:

- `gender`
- `age_group`
- `country_id`
- `min_age`
- `max_age`
- `min_gender_probability`
- `min_country_probability`
- `sort_by`
- `order`
- `page`
- `limit`

Paginated response includes:

- `page`
- `limit`
- `total`
- `total_pages`
- `links.self`
- `links.next`
- `links.prev`

### Search profiles with natural language

`GET /api/profiles/search?q=...`

Admin and analyst.

This uses the deterministic `NlParser` and does not use an LLM.

Supported parsing rules include:

- gender words like `male`, `female`, `men`, `women`
- age phrases like `above 30`, `below 18`, `between 20 and 40`
- age groups like `child`, `teenager`, `adult`, `senior`
- `young` mapping to ages `16-24`
- country names resolved from seed data

Examples:

- `young males from nigeria`
- `females above 30`
- `adult males from kenya`

### Export profiles

`GET /api/profiles/export?format=csv`

Admin and analyst.

The export reuses the same filters and sorting behavior as `GET /api/profiles`.

Response:

- content type `text/csv`
- downloadable file name `profiles_<timestamp>.csv`

CSV column order:

- `id`
- `name`
- `gender`
- `gender_probability`
- `age`
- `age_group`
- `country_id`
- `country_name`
- `country_probability`
- `created_at`

### Delete profile

`DELETE /api/profiles/{id}`

Admin only.

## Natural Language Parsing Approach

Natural-language search is implemented with a deterministic parser, not a generative model. The parser maps recognized phrases into structured filter options that reuse the same query pipeline as the normal list endpoint.

This means:

- search behavior is predictable
- profile search stays consistent across API, CLI, and web
- results follow the same filtering, sorting, and pagination rules as structured requests

## Multi-Interface Contract

For the CLI client:

- create a PKCE `code_verifier` and `code_challenge`
- call `/auth/github?mode=cli...`
- open the returned `authorize_url`
- complete callback handling with `code_verifier`
- store returned access and refresh tokens locally
- call `/auth/refresh` when the access token expires

For the web client:

- send users to `/auth/github`
- optionally supply `client_redirect_uri` and client `state`
- rely on HTTP-only cookies for the session
- send `X-CSRF-Token` on cookie-authenticated refresh, logout, and other state-changing operations

For both clients:

- send `X-API-Version: 1` on all `/api/profiles*` requests
- treat access tokens as short-lived
- treat refresh tokens as session credentials
- consume the same profile list, search, detail, and export endpoints

## Rate Limits

Current rate limits:

- `/auth/*`: `10` requests per minute
- `/api/*`: `60` requests per minute per authenticated user

If a limit is exceeded, the API returns:

```json
{
  "status": "error",
  "message": "Rate limit exceeded"
}
```

## Request Logging

Each request logs:

- HTTP method
- path
- status code
- response time in milliseconds

## Error Format

Auth, versioning, rate limit, and authorization paths use:

```json
{
  "status": "error",
  "message": "message"
}
```

Examples:

- `401` for missing or invalid authentication
- `403` for forbidden or inactive users
- `400` for invalid API version header
- `422` for invalid query parameters
- `429` for rate limiting

## CI

This repository includes GitHub Actions in `.github/workflows/backend-ci.yml`.

The workflow runs on `push` and `pull_request` to `main` and `master`, and performs:

- `dotnet restore`
- `dotnet format --verify-no-changes`
- `dotnet build --configuration Release`
- `dotnet test --configuration Release`

## Notes

- all timestamps are stored and returned in UTC ISO 8601 format
- IDs are generated as UUID v7 strings
- secret values should be provided through user-secrets or environment variables, not committed to git
