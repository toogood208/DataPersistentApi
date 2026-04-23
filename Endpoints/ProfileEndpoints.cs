using DataPersistentApi.Services;

namespace DataPersistentApi.Endpoints;

public static class ProfileEndpoints
{
    // DTO for create request
    public record CreateProfileRequest(string? name);

    public static void MapProfileEndpoints(this WebApplication app)
    {
        // POST /api/profiles
        app.MapPost("/api/profiles", async (CreateProfileRequest? req, ProfileService svc) =>
        {
            if (req == null)
            {
                return Results.UnprocessableEntity(new { status = "error", message = "Invalid type" });
            }

            if (string.IsNullOrWhiteSpace(req.name))
            {
                return Results.BadRequest(new { status = "error", message = "Missing or empty name" });
            }

            var result = await svc.CreateOrGetAsync(req.name!);
            if (result.IsError)
            {
                // Map specific 502 messages per spec
                var body = new { status = "error", message = result.Message };
                return Results.Json(body, statusCode: result.StatusCode);
            }

            if (result.AlreadyExists && result.Profile != null)
            {
                var p = result.Profile;
                return Results.Ok(new
                {
                    status = "success",
                    message = "Profile already exists",
                    data = new
                    {
                        id = p.Id,
                        name = p.Name,
                        gender = p.Gender,
                        gender_probability = p.GenderProbability,
                        sample_size = p.SampleSize,
                        age = p.Age,
                        age_group = p.AgeGroup,
                        country_id = p.CountryId,
                        country_probability = p.CountryProbability,
                        created_at = p.CreatedAt.ToString("o")
                    }
                });
            }

            // created
            var created = result.Profile!;
            return Results.Created($"/api/profiles/{created.Id}", new
            {
                status = "success",
                data = new
                {
                    id = created.Id,
                    name = created.Name,
                    gender = created.Gender,
                    gender_probability = created.GenderProbability,
                    sample_size = created.SampleSize,
                    age = created.Age,
                    age_group = created.AgeGroup,
                    country_id = created.CountryId,
                    country_probability = created.CountryProbability,
                    created_at = created.CreatedAt.ToString("o")
                }
            });
        });

        // GET /api/profiles/{id}
        app.MapGet("/api/profiles/{id}", async (string id, ProfileService svc) =>
        {
            var p = await svc.GetByIdAsync(id);
            if (p == null)
                return Results.NotFound(new { status = "error", message = "Profile not found" });

            return Results.Ok(new
            {
                status = "success",
                data = new
                {
                    id = p.Id,
                    name = p.Name,
                    gender = p.Gender,
                    gender_probability = p.GenderProbability,
                    sample_size = p.SampleSize,
                    age = p.Age,
                    age_group = p.AgeGroup,
                    country_id = p.CountryId,
                    country_probability = p.CountryProbability,
                    created_at = p.CreatedAt.ToString("o")
                }
            });
        });

        // GET /api/profiles
        app.MapGet("/api/profiles", async (HttpRequest req, ProfilesQueryService svc) =>
        {
            // parse query params
            var q = req.Query;
            var opts = new QueryOptions();

            // parse strings
            opts.Gender = q["gender"].FirstOrDefault();
            opts.AgeGroup = q["age_group"].FirstOrDefault();
            opts.CountryId = q["country_id"].FirstOrDefault();

    // numeric parsing with 422 on invalid
    if (q.TryGetValue("min_age", out var maStr))
    {
        if (!int.TryParse(maStr, out var maVal)) return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
        opts.MinAge = maVal;
    }

    if (q.TryGetValue("max_age", out var xaStr))
    {
        if (!int.TryParse(xaStr, out var xaVal)) return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
        opts.MaxAge = xaVal;
    }

    if (q.TryGetValue("min_gender_probability", out var mgpStr))
    {
        if (!double.TryParse(mgpStr, out var mgpVal)) return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
        opts.MinGenderProbability = mgpVal;
    }

    if (q.TryGetValue("min_country_probability", out var mcpStr))
    {
        if (!double.TryParse(mcpStr, out var mcpVal)) return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
        opts.MinCountryProbability = mcpVal;
    }

    // sort & pagination
    opts.SortBy = q["sort_by"].FirstOrDefault();
    opts.Order = q["order"].FirstOrDefault();
            // validate sort_by and order
            var validSort = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "age", "created_at", "gender_probability" };
            var validOrder = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "asc", "desc" };
            if (!string.IsNullOrWhiteSpace(opts.SortBy) && !validSort.Contains(opts.SortBy))
                return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
            if (!string.IsNullOrWhiteSpace(opts.Order) && !validOrder.Contains(opts.Order))
                return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
    if (q.TryGetValue("page", out var pStr))
    {
        if (!int.TryParse(pStr, out var pageVal) || pageVal < 1) return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
        opts.Page = pageVal;
    }
    if (q.TryGetValue("limit", out var lStr))
    {
        if (!int.TryParse(lStr, out var limitVal) || limitVal < 1 || limitVal > 50) return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
        opts.Limit = limitVal;
    }

            var (total, items) = await svc.QueryAsync(opts);
            // format created_at to ISO
            var data = items.Select(i => {
                var d = (dynamic)i;
                return new {
                    id = d.id,
                    name = d.name,
                    gender = d.gender,
                    gender_probability = d.gender_probability,
                    age = d.age,
                    age_group = d.age_group,
                    country_id = d.country_id,
                    country_name = d.country_name,
                    country_probability = d.country_probability,
                    created_at = ((DateTime)d.created_at).ToString("o")
                };
            }).ToList();

            return Results.Ok(new { status = "success", page = opts.Page, limit = opts.Limit, total, data });
        });

        // GET /api/profiles/search?q=...
        app.MapGet("/api/profiles/search", async (HttpRequest req, NlParser parser, ProfilesQueryService svc) =>
        {
            var q = req.Query["q"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { status="error", message="Missing or empty parameter" });

            if (!parser.TryParse(q!, out var opts)) return Results.BadRequest(new { status="error", message="Unable to interpret query" });

    // pagination & optional sort support same as above
    if (req.Query.TryGetValue("sort_by", out var sb))
    {
        var sortVal = sb.FirstOrDefault();
        var validSort = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "age", "created_at", "gender_probability" };
        if (!string.IsNullOrWhiteSpace(sortVal) && !validSort.Contains(sortVal)) return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
        opts.SortBy = sortVal;
    }
    if (req.Query.TryGetValue("order", out var ord))
    {
        var orderVal = ord.FirstOrDefault();
        var validOrder = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "asc", "desc" };
        if (!string.IsNullOrWhiteSpace(orderVal) && !validOrder.Contains(orderVal)) return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
        opts.Order = orderVal;
    }
    if (req.Query.TryGetValue("page", out var pageStr))
    {
        if (!int.TryParse(pageStr, out var pageVal) || pageVal < 1) return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
        opts.Page = pageVal;
    }
    if (req.Query.TryGetValue("limit", out var limitStr))
    {
        if (!int.TryParse(limitStr, out var limitVal) || limitVal < 1 || limitVal > 50) return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
        opts.Limit = limitVal;
    }

            var (total, items) = await svc.QueryAsync(opts);
            var data = items.Select(i => {
                var d = (dynamic)i;
                return new {
                    id = d.id,
                    name = d.name,
                    gender = d.gender,
                    gender_probability = d.gender_probability,
                    age = d.age,
                    age_group = d.age_group,
                    country_id = d.country_id,
                    country_name = d.country_name,
                    country_probability = d.country_probability,
                    created_at = ((DateTime)d.created_at).ToString("o")
                };
            }).ToList();

            return Results.Ok(new { status = "success", page = opts.Page, limit = opts.Limit, total, data });
        });

        // DELETE /api/profiles/{id}
        app.MapDelete("/api/profiles/{id}", async (string id, ProfileService svc) =>
        {
            var ok = await svc.DeleteAsync(id);
            if (!ok) return Results.NotFound(new { status = "error", message = "Profile not found" });
            return Results.NoContent();
        });
    }
}
