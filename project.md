# Insighta Labs+ Stage 3 Project Plan

## Working Agreement

This project will be completed one step at a time.

Rules for how we will work:

1. I will only propose code for the current step.
2. For each step, I will provide:
   - the code snippet
   - where it should go
   - why this is the best approach for the current codebase
3. We will not proceed to the next step until the current step is implemented and verified.
4. We will protect all Stage 2 features from regression:
   - filtering
   - sorting
   - pagination
   - natural language querying

## Goal

Turn this backend into the `insighta-backend` repository for Stage 3 by adding:

- GitHub OAuth authentication
- PKCE support for CLI login
- access and refresh token handling
- role-based access control
- secured `/api/*` endpoints
- API version enforcement
- CSV export
- rate limiting
- request logging
- updated documentation

## Current Repo Role

This repository is the backend service and should remain the single source of truth for:

- authentication
- authorization
- token issuance and refresh
- profile creation and querying
- natural language search
- response shape consistency

The CLI and web portal will be separate repositories that consume this backend.

## Step-by-Step Plan

## Step 1: Establish Stage 3 Backend Foundation

Objective:
Prepare the backend to support authentication and authorization without changing existing profile behavior yet.

Changes:

- add required NuGet packages for authentication support
- add configuration sections for GitHub OAuth and token settings
- prepare service registration structure in `Program.cs`

Why first:
This creates the foundation for all later steps and avoids mixing auth logic directly into the existing profile handlers too early.

Done when:

- packages are installed
- config keys are defined
- `Program.cs` is ready for auth service wiring
- existing API still runs

## Step 2: Add User and Refresh Token Persistence

Objective:
Create the database structures needed for identity and session management.

Changes:

- add `User` model
- add `RefreshToken` model
- update `AppDBContext`
- add entity configuration
- create EF migration

Why here:
Authentication flows should not be built before the persistence model is stable.

Done when:

- users table exists
- refresh tokens table exists
- indexes and constraints are in place

## Step 3: Build Token Infrastructure

Objective:
Create the internal services that issue, validate, rotate, and revoke tokens.

Changes:

- add access token generation service
- add refresh token hashing and storage service
- add refresh token rotation logic
- define auth response DTOs

Why here:
Both CLI and web authentication depend on one shared token system.

Done when:

- access tokens can be issued
- refresh tokens are stored hashed
- refresh token rotation works
- old refresh tokens are invalidated on use

## Step 4: Implement GitHub OAuth Backend Flow

Objective:
Support login using GitHub for both browser and CLI-driven PKCE flows.

Changes:

- add `GET /auth/github`
- add `GET /auth/github/callback`
- add backend GitHub exchange service
- add user upsert logic from GitHub profile data

Why here:
OAuth should sit on top of the persistence and token infrastructure from Steps 2 and 3.

Done when:

- web login flow works through backend callback
- backend can accept CLI OAuth exchange inputs
- user record is created or updated

## Step 5: Add Refresh and Logout Endpoints

Objective:
Complete the session lifecycle.

Changes:

- add `POST /auth/refresh`
- add `POST /auth/logout`

Why here:
Login without refresh and logout is incomplete and will block the CLI and web portal.

Done when:

- refresh issues a new token pair
- old refresh token becomes invalid immediately
- logout revokes the provided refresh token

## Step 6: Add Authentication and Role-Based Authorization

Objective:
Protect all `/api/*` endpoints in a structured way.

Changes:

- add authentication middleware
- add authorization policies
- add admin and analyst role rules
- add inactive-user enforcement

Why here:
This is where the current open API becomes a secure API without scattering checks in every handler.

Done when:

- all `/api/*` routes require authentication
- analysts can only read/search
- admins can create/delete/query
- inactive users receive `403`

## Step 7: Apply API Version Enforcement

Objective:
Require `X-API-Version: 1` for profile endpoints.

Changes:

- add reusable version enforcement for profile route group

Why here:
This is a cross-cutting API rule and should be applied consistently after route protection is in place.

Done when:

- requests without `X-API-Version: 1` return `400`
- error format matches project requirement

## Step 8: Update Paginated Response Shape

Objective:
Bring list and search responses in line with Stage 3 requirements.

Changes:

- add `total_pages`
- add `links.self`
- add `links.next`
- add `links.prev`
- keep existing filtering, sorting, pagination, and natural language behavior intact

Why here:
This improves the API contract while keeping the existing query engine unchanged.

Done when:

- `GET /api/profiles`
- `GET /api/profiles/search`

both return the new pagination payload correctly.

## Step 9: Add CSV Export Endpoint

Objective:
Support export using the same query rules as the list endpoint.

Changes:

- add `GET /api/profiles/export?format=csv`
- reuse filters and sorting logic
- return downloadable CSV response

Why here:
Export should depend on the stabilized query behavior from Step 8.

Done when:

- CSV is returned with correct headers
- export respects filters and sorting

## Step 10: Add Rate Limiting and Request Logging

Objective:
Add operational protections and observability.

Changes:

- add rate limiting for `/auth/*`
- add per-user rate limiting for other endpoints
- add request logging middleware

Why here:
These are production protections best added after the main API behavior is stable.

Done when:

- auth endpoints are limited to 10 requests/minute
- other endpoints are limited to 60 requests/minute per user
- each request logs method, path, status code, and response time

## Step 11: Strengthen Error Response Consistency

Objective:
Ensure all new backend features use the required error format.

Changes:

- standardize new error responses to:

```json
{
  "status": "error",
  "message": "message"
}
```

Why here:
By this point most new paths exist, so consistency cleanup is safer and easier.

Done when:

- auth errors follow the standard format
- versioning errors follow the standard format
- authorization and validation responses are aligned as much as practical

## Step 12: Documentation and Handoff Readiness

Objective:
Prepare this backend for integration with the CLI and web repos.

Changes:

- update `README.md`
- document env vars
- document auth flow
- document token lifecycle
- document roles and endpoint protection
- document API usage expectations for CLI and web

Why here:
Documentation should reflect the final implemented behavior, not a moving target.

Done when:

- README is complete for Stage 3 backend scope
- integration expectations are clear for the other two repos

## Guardrails

During every step:

- do not rewrite the query engine unless necessary
- do not duplicate logic across endpoints
- prefer services, policies, middleware, and reusable helpers
- keep profile querying behavior backward-compatible except where Stage 3 explicitly changes the contract
- verify current step before moving forward

## Definition of Progress

We only move to the next step when:

1. the current code change is implemented
2. it is explained clearly
3. it passes the agreed verification for that step
4. you confirm we should continue
