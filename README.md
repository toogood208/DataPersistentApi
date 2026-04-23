Natural language parsing

This service uses a rule-based, deterministic parser (`NlParser`) to translate short English queries into the same filter parameters used by `GET /api/profiles`. No AI or external LLM is used.

Supported keywords and mappings
- Gender keywords: `"male"`, `"males"`, `"men"`, `"boy"`, `"boys"` map to `gender=male`.
- Gender keywords: `"female"`, `"females"`, `"women"`, `"girl"`, `"girls"` map to `gender=female`.
- Mixed gender phrases such as `"male and female teenagers above 17"` do not apply a gender filter; they only apply the non-gender filters that can be interpreted.
- `"young"` maps to `min_age=16` and `max_age=24`. This is a parsing rule only; it is not stored as an `age_group`.
- `"above N"` and `"over N"` map to `min_age=N`.
- `"below N"` and `"under N"` map to `max_age=N`.
- `"between A and B"` maps to `min_age=min(A,B)` and `max_age=max(A,B)`.
- `"child"` and `"children"` map to `age_group=child`.
- `"teen"` and `"teenager"` map to `age_group=teenager`.
- `"adult"` maps to `age_group=adult`.
- `"senior"` and `"elder"` map to `age_group=senior`.
- Country names are matched against the seed-derived country map loaded at startup and converted to ISO-2 `country_id` values such as `NG`, `KE`, and `AO`.

Examples
- `"young males from nigeria"` becomes `gender=male`, `min_age=16`, `max_age=24`, `country_id=NG`.
- `"females above 30"` becomes `gender=female`, `min_age=30`.
- `"people from angola"` becomes `country_id=AO`.
- `"adult males from kenya"` becomes `gender=male`, `age_group=adult`, `country_id=KE`.
- `"male and female teenagers above 17"` becomes `age_group=teenager`, `min_age=17`.

How the parser works
- The query string is normalized to lowercase.
- Regex rules extract age phrases like `above`, `below`, `over`, `under`, and `between`.
- Keyword checks detect explicit age groups and gender tokens.
- Country names are matched by scanning the country-name-to-ISO map loaded from `Data/seed/profiles-2026.json`.
- If no supported filter can be extracted, the endpoint returns `{ "status": "error", "message": "Unable to interpret query" }`.

Limitations
- The parser is intentionally strict and rule-based. It does not support fuzzy matching, spelling correction, stemming, or free-form semantic understanding.
- It does not support complex boolean groupings such as parentheses, explicit `OR`, or `NOT`.
- Country recognition depends on the country names present in the seed file loaded at startup.
- Natural-language phrases like `"late twenties"` or `"middle aged"` are not supported.
- If a query mixes supported and unsupported phrases, only the supported parts are used.

Seeding behavior
- The seed loader accepts either a raw JSON array or an object with a top-level `"profiles"` array.
- Seeding is idempotent: profiles are skipped if a record with the same normalized name already exists.
- Re-running the seed does not create duplicates.

Validation behavior
- Invalid numeric parameters such as non-integer `page`, `limit`, `min_age`, or `max_age` return `422 Unprocessable Entity` with `{ "status": "error", "message": "Invalid query parameters" }`.
- Invalid `sort_by` or `order` values also return the same `422` error payload.

How to seed locally
- In PowerShell:
```powershell
$env:SEED = "true"
dotnet run
```

Notes
- All timestamps are stored and returned in UTC ISO 8601 format.
- IDs are generated as UUID v7 strings.
- CORS is configured to allow any origin with `Access-Control-Allow-Origin: *`.
