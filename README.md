# Insighta Backend

`Insighta Backend` is the Stage 3 backend service for profile generation, querying, authentication, authorization, token lifecycle management, and export.

This repository is the backend source of truth for:

- GitHub OAuth login
- PKCE-friendly CLI login initiation
- JWT access token issuance
- refresh token rotation and revocation
- role-based access control
- protected profile APIs
- profile filtering, sorting, pagination, and natural-language search
- CSV export

The CLI app and web portal are expected to consume this API as separate clients.

## Tech Stack

- .NET `10.0`
- ASP.NET Core minimal APIs
- Entity Framework Core
- SQL Server or EF InMemory fallback
- JWT bearer authentication

## Configuration

The app reads settings from `appsettings.json`.

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

- Set a real `SigningKey` outside development.
- Access tokens are short-lived JWTs.
- Refresh tokens are stored hashed in the database.

## Running Locally

Restore and run:

```powershell
dotnet restore
dotnet run
```

Seed profiles locally:

```powershell
$env:SEED = "true"
dotnet run
```

Seeding notes:

- The seed loader accepts either a raw JSON array or an object with a top-level `profiles` array.
- Seeding is idempotent by normalized profile name.

## Authentication

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
4. issues an access token and refresh token

If `client_redirect_uri` was provided, the backend redirects back to that client with token data in the URL fragment.

### CLI / PKCE login

Start CLI login:

`GET /auth/github?mode=cli&code_challenge=...&code_challenge_method=S256`

Optional query parameters:

- `client_redirect_uri`
- `state`

The backend returns JSON containing:

- `authorize_url`
- protected `state`

Complete the flow with:

`GET /auth/github/callback?code=...&state=...&code_verifier=...`

### Refresh tokens

Refresh:

`POST /auth/refresh`

```json
{
  "refreshToken": "..."
}
```

Logout:

`POST /auth/logout`

```json
{
  "refreshToken": "..."
}
```

Token lifecycle:

- refresh tokens are hashed before storage
- refresh uses rotation
- the old refresh token is revoked immediately when exchanged
- logout revokes the provided refresh token

## Authorization

Roles:

- `admin`
- `analyst`

Active-state enforcement:

- users must exist in the database
- inactive users are denied with `403`

Policy rules:

- `admin` can create, list, export, and delete profiles
- `admin` and `analyst` can read individual profiles and use natural-language search

## API Requirements

### Authentication

All `/api/profiles*` endpoints require a bearer token.

Example header:

```http
Authorization: Bearer <access-token>
```

### API version header

All `/api/profiles*` endpoints require:

```http
X-API-Version: 1
```

Requests without that exact header return:

```json
{
  "status": "error",
  "message": "Missing or invalid X-API-Version"
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

### Get profile by id

`GET /api/profiles/{id}`

Admin and analyst.

### List profiles

`GET /api/profiles`

Admin only.

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

Pagination response includes:

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

Admin only.

The export reuses the same filters and sorting behavior as `GET /api/profiles`.

Response:

- content type `text/csv`
- downloadable file name `profiles.csv`

### Delete profile

`DELETE /api/profiles/{id}`

Admin only.

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

Newer auth, versioning, rate limit, and authorization paths use:

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

## Integration Expectations

For the web client:

- send users to `/auth/github`
- optionally supply `client_redirect_uri` and client `state`
- read tokens from the redirect fragment after callback

For the CLI client:

- create a PKCE `code_verifier` and `code_challenge`
- call `/auth/github?mode=cli...`
- open the returned `authorize_url`
- complete callback handling with `code_verifier`
- store and later rotate refresh tokens through `/auth/refresh`

For both clients:

- send `Authorization: Bearer <token>` on protected profile routes
- send `X-API-Version: 1` on all `/api/profiles*` requests
- treat access tokens as short-lived
- treat refresh tokens as session credentials

## Notes

- All timestamps are stored and returned in UTC ISO 8601 format.
- IDs are generated as UUID v7 strings.
- CORS is configured with `Access-Control-Allow-Origin: *`.
