using System.Globalization;
using System.Text;
using DataPersistentApi.Configuration;
using DataPersistentApi.Models;
using DataPersistentApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace DataPersistentApi.Endpoints;

public static class ProfileEndpoints
{
    // DTO for create request
    public record CreateProfileRequest(string? name);

    public static void MapProfileEndpoints(this WebApplication app)
    {
        var profiles = app.MapGroup("/api/profiles")
            .RequireAuthorization()
            .RequireRateLimiting(StartupConstants.ApiRateLimitPolicy)
            .AddEndpointFilter(new ApiVersionEndpointFilter(StartupConstants.ApiVersionHeader, StartupConstants.ApiVersionValue));
        var csrfFilter = new CsrfEndpointFilter(AuthCookieService.CsrfCookieName, AuthCookieService.CsrfHeaderName);

        // POST /api/profiles
        profiles.MapPost("", async (CreateProfileRequest? req, ProfileService svc) =>
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
                        age = p.Age,
                        age_group = p.AgeGroup,
                        country_id = p.CountryId,
                        country_name = p.CountryName,
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
                    age = created.Age,
                    age_group = created.AgeGroup,
                    country_id = created.CountryId,
                    country_name = created.CountryName,
                    country_probability = created.CountryProbability,
                    created_at = created.CreatedAt.ToString("o")
                }
            });
        }).RequireAuthorization(StartupConstants.AdminOnlyPolicy).AddEndpointFilter(csrfFilter);

        // GET /api/profiles/{id}
        profiles.MapGet("/{id}", async (string id, ProfileService svc) =>
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
                    age = p.Age,
                    age_group = p.AgeGroup,
                    country_id = p.CountryId,
                    country_name = p.CountryName,
                    country_probability = p.CountryProbability,
                    created_at = p.CreatedAt.ToString("o")
                }
            });
        }).RequireAuthorization(StartupConstants.ReadAccessPolicy);

        // GET /api/profiles
        profiles.MapGet("", async (HttpRequest req, ProfilesQueryService svc) =>
        {
            var parseResult = TryParseProfilesQuery(req.Query);
            if (parseResult.IsError)
            {
                return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
            }

            var opts = parseResult.Options!;

            var (total, items) = await svc.QueryAsync(opts);
            return Results.Ok(CreatePagedResponse(req, opts, total, items));
        }).RequireAuthorization(StartupConstants.AdminOnlyPolicy);

        // GET /api/profiles/search?q=...
        profiles.MapGet("/search", async (HttpRequest req, [FromServices] NlParser parser, ProfilesQueryService svc) =>
        {
            var q = req.Query["q"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { status="error", message="Missing or empty parameter" });

            if (!parser.TryParse(q!, out var opts)) return Results.BadRequest(new { status="error", message="Unable to interpret query" });

            var parseResult = TryApplyPagingAndSorting(req.Query, opts);
            if (parseResult.IsError)
            {
                return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
            }

            var (total, items) = await svc.QueryAsync(opts);
            return Results.Ok(CreatePagedResponse(req, opts, total, items));
        }).RequireAuthorization(StartupConstants.ReadAccessPolicy);

        // GET /api/profiles/export?format=csv
        profiles.MapGet("/export", async (HttpRequest req, ProfilesQueryService svc) =>
        {
            var format = req.Query["format"].FirstOrDefault();
            if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { status = "error", message = "Unsupported export format" });
            }

            var parseResult = TryParseProfilesQuery(req.Query);
            if (parseResult.IsError)
            {
                return Results.UnprocessableEntity(new { status = "error", message = "Invalid query parameters" });
            }

            var opts = parseResult.Options!;
            var items = await svc.QueryAllAsync(opts);
            var csv = BuildCsv(items);

            return Results.File(
                Encoding.UTF8.GetBytes(csv),
                "text/csv; charset=utf-8",
                $"profiles_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }).RequireAuthorization(StartupConstants.AdminOnlyPolicy);

        // DELETE /api/profiles/{id}
        profiles.MapDelete("/{id}", async (string id, ProfileService svc) =>
        {
            var ok = await svc.DeleteAsync(id);
            if (!ok) return Results.NotFound(new { status = "error", message = "Profile not found" });
            return Results.NoContent();
        }).RequireAuthorization(StartupConstants.AdminOnlyPolicy).AddEndpointFilter(csrfFilter);
    }

    private static ParseQueryResult TryParseProfilesQuery(IQueryCollection query)
    {
        var opts = new QueryOptions
        {
            Gender = query["gender"].FirstOrDefault()?.Trim().ToLowerInvariant(),
            AgeGroup = query["age_group"].FirstOrDefault()?.Trim().ToLowerInvariant(),
            CountryId = query["country_id"].FirstOrDefault()?.Trim().ToUpperInvariant()
        };

        if (query.TryGetValue("min_age", out var minAgeStr))
        {
            if (!int.TryParse(minAgeStr, out var minAge))
            {
                return ParseQueryResult.Error();
            }

            opts.MinAge = minAge;
        }

        if (query.TryGetValue("max_age", out var maxAgeStr))
        {
            if (!int.TryParse(maxAgeStr, out var maxAge))
            {
                return ParseQueryResult.Error();
            }

            opts.MaxAge = maxAge;
        }

        if (query.TryGetValue("min_gender_probability", out var minGenderProbabilityStr))
        {
            if (!double.TryParse(minGenderProbabilityStr, out var minGenderProbability))
            {
                return ParseQueryResult.Error();
            }

            opts.MinGenderProbability = minGenderProbability;
        }

        if (query.TryGetValue("min_country_probability", out var minCountryProbabilityStr))
        {
            if (!double.TryParse(minCountryProbabilityStr, out var minCountryProbability))
            {
                return ParseQueryResult.Error();
            }

            opts.MinCountryProbability = minCountryProbability;
        }

        return TryApplyPagingAndSorting(query, opts);
    }

    private static ParseQueryResult TryApplyPagingAndSorting(IQueryCollection query, QueryOptions opts)
    {
        opts.SortBy = query["sort_by"].FirstOrDefault();
        opts.Order = query["order"].FirstOrDefault();

        var validSort = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "age", "created_at", "gender_probability" };
        var validOrder = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "asc", "desc" };

        if (!string.IsNullOrWhiteSpace(opts.SortBy) && !validSort.Contains(opts.SortBy))
        {
            return ParseQueryResult.Error();
        }

        if (!string.IsNullOrWhiteSpace(opts.Order) && !validOrder.Contains(opts.Order))
        {
            return ParseQueryResult.Error();
        }

        if (query.TryGetValue("page", out var pageStr))
        {
            if (!int.TryParse(pageStr, out var page) || page < 1)
            {
                return ParseQueryResult.Error();
            }

            opts.Page = page;
        }

        if (query.TryGetValue("limit", out var limitStr))
        {
            if (!int.TryParse(limitStr, out var limit) || limit < 1 || limit > 50)
            {
                return ParseQueryResult.Error();
            }

            opts.Limit = limit;
        }

        return ParseQueryResult.Success(opts);
    }

    private static object CreatePagedResponse(HttpRequest req, QueryOptions opts, int total, List<ProfileListItemDto> items)
    {
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)opts.Limit);
        var data = items.Select(SerializeProfileListItem).ToList();

        return new
        {
            status = "success",
            page = opts.Page,
            limit = opts.Limit,
            total,
            total_pages = totalPages,
            links = new
            {
                self = BuildPageLink(req, opts.Page, opts.Limit),
                next = totalPages > 0 && opts.Page < totalPages ? BuildPageLink(req, opts.Page + 1, opts.Limit) : null,
                prev = opts.Page > 1 && totalPages > 0 ? BuildPageLink(req, opts.Page - 1, opts.Limit) : null
            },
            data
        };
    }

    private static object SerializeProfileListItem(ProfileListItemDto item)
    {
        return new
        {
            id = item.Id,
            name = item.Name,
            gender = item.Gender,
            gender_probability = item.GenderProbability,
            age = item.Age,
            age_group = item.AgeGroup,
            country_id = item.CountryId,
            country_name = item.CountryName,
            country_probability = item.CountryProbability,
            created_at = item.CreatedAt.ToString("o")
        };
    }

    private static string BuildCsv(IEnumerable<ProfileListItemDto> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("id,name,gender,gender_probability,age,age_group,country_id,country_name,country_probability,created_at");

        foreach (var item in items)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                EscapeCsv(item.Id),
                EscapeCsv(item.Name),
                EscapeCsv(item.Gender),
                item.GenderProbability.ToString(CultureInfo.InvariantCulture),
                item.Age.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(item.AgeGroup),
                EscapeCsv(item.CountryId),
                EscapeCsv(item.CountryName),
                item.CountryProbability.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(item.CreatedAt.ToString("o"))
            }));
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        var safeValue = value ?? string.Empty;
        if (!safeValue.Contains(',') && !safeValue.Contains('"') && !safeValue.Contains('\n') && !safeValue.Contains('\r'))
        {
            return safeValue;
        }

        return $"\"{safeValue.Replace("\"", "\"\"")}\"";
    }

    private static string BuildPageLink(HttpRequest req, int page, int limit)
    {
        var query = req.Query.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        query["page"] = page.ToString();
        query["limit"] = limit.ToString();

        return QueryHelpers.AddQueryString($"{req.Scheme}://{req.Host}{req.PathBase}{req.Path}", query!);
    }

    private record ParseQueryResult(bool IsError, QueryOptions? Options)
    {
        public static ParseQueryResult Success(QueryOptions opts) => new(false, opts);
        public static ParseQueryResult Error() => new(true, null);
    }
}
