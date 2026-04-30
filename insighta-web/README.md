# Insighta Web

`Insighta Web` is a standalone-ready web portal for Insighta Labs+. It currently lives inside this workspace so it can be developed against the backend, but it is intentionally isolated so it can be moved into a separate repository later.

## Features

- GitHub OAuth login through the shared backend
- cookie-based session handling with `credentials: "include"`
- CSRF-aware write operations
- dashboard metrics
- profiles list with filters, pagination, export, and admin-only create
- profile detail page
- natural-language search page
- account page with logout

## Local Run

```powershell
$env:INSIGHTA_API_URL = "https://your-backend.example.com"
npm start
```

The local server defaults to:

- web portal: `http://localhost:4173`
- backend: `http://localhost:5000` if `INSIGHTA_API_URL` is not set

## Environment Variables

- `INSIGHTA_API_URL`: base URL for the backend API
- `PORT`: local web server port for the portal

## Backend Requirements

For browser login to work correctly, the backend must allow the portal origin in its CORS configuration and support credentialed requests for that origin.

## Architecture Notes

- the backend URL is injected at runtime from `server.mjs`
- no backend source files are imported here
- HTTP-only auth cookies remain inaccessible to JavaScript
- the frontend reads only the CSRF cookie and sends `X-CSRF-Token` for state-changing requests

## Extraction To Another Repo

This folder is self-contained:

- its own `package.json`
- its own `README`
- its own CI workflow
- no source-level dependency on the backend project

That means it can be copied to a new repo directly or extracted later with git history tools.
