Natural language parsing

This service uses a rule-based, deterministic parser (NlParser) to translate short English queries into the same filter parameters used by the /api/profiles endpoint. No AI or external LLM is used.

Supported keywords and mappings
- Gender keywords: "male", "males", "men", "boy", "boys" 12-> gender=male; "female", "females", "women", "girls" -> gender=female
- Age keywords:
  - "young" -> min_age=16 and max_age=24 (this is a parsing rule only; not stored as a special age_group)
  - "above N" or "over N" -> min_age=N
  - "below N" or "under N" -> max_age=N
  - "between A and B" -> min_age=A and max_age=B
  - explicit age groups: "child"/"children" -> age_group=child; "teen"/"teenager" -> age_group=teenager; "adult" -> age_group=adult; "senior"/"elder" -> age_group=senior
- Country: the parser looks for country names (e.g. "nigeria", "kenya", "angola") and maps them to ISO2 codes (country_id). The server loads country_name->country_id mappings from the seed file (Data/seed/profiles-2026.json) when available.

Examples
- "young males from nigeria" -> gender=male + min_age=16 + max_age=24 + country_id=NG
- "females above 30" -> gender=female + min_age=30
- "adult males from kenya" -> gender=male + age_group=adult + country_id=KE

Limitations and edge cases
- The parser is purely rule-based and intentionally limited. It does not perform fuzzy matching, stemming, synonyms beyond the hard-coded tokens, or complex boolean logic (no OR/NOT groups). Phrases it cannot interpret return an error.
- Country recognition depends on entries present in Data/seed/profiles-2026.json (loaded at startup). If a country name is not present in that file, queries using that country name will not be recognized.
- Numeric phrases must follow simple patterns ("above N", "below N", "between A and B"). Natural-language phrases such as "late twenties" are not supported.
- When both explicit ages and age_group are present, both filters are applied (i.e., results must satisfy every condition).
- Parser does not infer pluralization contexts beyond the listed keywords (e.g., "male and female" may be ambiguous).

Seeding behavior
- The seed loader accepts the provided JSON structure { "profiles": [ ... ] } or a raw array. Missing created_at values default to the current UTC time. Seeding is idempotent (records are skipped if a name already exists, case-insensitive).

Validation behavior
- Invalid numeric parameters (non-integer page/limit/min_age/etc.) return 422 Unprocessable Entity with { "status": "error", "message": "Invalid query parameters" }.
- Invalid values for sort_by or order return 422 as well.

How to seed locally
- Run with environment variable SEED=true, e.g. in PowerShell:
  $env:SEED = 'true'
  dotnet run

Notes
- All timestamps are stored and returned in UTC (ISO 8601). IDs are generated as UUID v7.
- CORS is configured to allow any origin (Access-Control-Allow-Origin: *).
