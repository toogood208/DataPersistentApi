using DataPersistentApi.Models;
using DataPersistentApi.Services;
using Microsoft.AspNetCore.Http;

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

        // GET /api/profiles[?gender&country_id&age_group]
        app.MapGet("/api/profiles", async (HttpRequest req, ProfileService svc) =>
        {
            var gender = req.Query["gender"].FirstOrDefault();
            var country = req.Query["country_id"].FirstOrDefault();
            var ageGroup = req.Query["age_group"].FirstOrDefault();

            var list = await svc.GetAllAsync(gender, country, ageGroup);

            var data = list.Select(p => new
            {
                id = p.Id,
                name = p.Name,
                gender = p.Gender,
                age = p.Age,
                age_group = p.AgeGroup,
                country_id = p.CountryId
            }).ToList();

            return Results.Ok(new { status = "success", count = data.Count, data });
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
