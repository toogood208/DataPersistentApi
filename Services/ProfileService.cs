using System.Globalization;
using System.Net.Http.Json;
using DataPersistentApi.Data;
using DataPersistentApi.Models;
using DataPersistentApi.Utils;
using Microsoft.EntityFrameworkCore;

namespace DataPersistentApi.Services;

public class ProfileService
{
    private readonly AppDBContext _db;
    private readonly IHttpClientFactory _httpFactory;

    public ProfileService(AppDBContext db, IHttpClientFactory httpFactory)
    {
        _db = db;
        _httpFactory = httpFactory;
    }

    public record ServiceResult(bool IsError, int StatusCode, string? Message, bool AlreadyExists, Profile? Profile);

    public async Task<ServiceResult> CreateOrGetAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new ServiceResult(true, 400, "Missing or empty name", false, null);

        var trimmed = name.Trim();
        var normalizedName = trimmed.ToLowerInvariant();

        // idempotency: check existing by name (case-insensitive)
        var existing = await _db.Profiles.FirstOrDefaultAsync(p => p.Name == normalizedName);
        if (existing != null)
            return new ServiceResult(false, 200, null, true, existing);

        var client = _httpFactory.CreateClient();

        // start requests in parallel
        var genderTask = client.GetFromJsonAsync<GenderizeResponse>($"https://api.genderize.io?name={Uri.EscapeDataString(trimmed)}");
        var agifyTask = client.GetFromJsonAsync<AgifyResponse>($"https://api.agify.io?name={Uri.EscapeDataString(trimmed)}");
        var natTask = client.GetFromJsonAsync<NationalizeResponse>($"https://api.nationalize.io?name={Uri.EscapeDataString(trimmed)}");

        try
        {
            await Task.WhenAll(genderTask, agifyTask, natTask);
        }
        catch (HttpRequestException)
        {
            return new ServiceResult(true, 502, "Upstream failure", false, null);
        }

        var genderRes = await genderTask;
        var agifyRes = await agifyTask;
        var natRes = await natTask;

        // Validate Genderize
        if (genderRes == null || string.IsNullOrEmpty(genderRes.Gender) || (genderRes.Count ?? 0) == 0)
            return new ServiceResult(true, 502, "Genderize returned an invalid response", false, null);

        // Validate Agify
        if (agifyRes == null || agifyRes.Age == null)
            return new ServiceResult(true, 502, "Agify returned an invalid response", false, null);

        // Validate Nationalize
        if (natRes == null || natRes.Country == null || !natRes.Country.Any())
            return new ServiceResult(true, 502, "Nationalize returned an invalid response", false, null);

        // Determine top country
        var topCountry = natRes.Country.OrderByDescending(c => c.Probability).First();

        // Classify age group
        int age = agifyRes.Age.Value;
        string ageGroup = ClassifyAgeGroup(age);

        // Build profile entity
        var profile = new Profile
        {
            Id = UuidV7Generator.NewUuidV7(),
            Name = normalizedName,
            Gender = (genderRes.Gender ?? string.Empty).ToLowerInvariant(),
            GenderProbability = genderRes.Probability ?? 0.0,
            Age = age,
            AgeGroup = ageGroup,
            CountryId = topCountry.Country_id.ToUpperInvariant(),
            CountryName = ResolveCountryName(topCountry.Country_id),
            CountryProbability = topCountry.Probability,
            CreatedAt = DateTime.UtcNow
        };

        _db.Profiles.Add(profile);
        await _db.SaveChangesAsync();

        return new ServiceResult(false, 201, null, false, profile);
    }

    public async Task<Profile?> GetByIdAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return await _db.Profiles.FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Profile>> GetAllAsync(string? gender, string? countryId, string? ageGroup)
    {
        var q = _db.Profiles.AsQueryable();
        if (!string.IsNullOrWhiteSpace(gender))
        {
            var g = gender.Trim().ToLowerInvariant();
            q = q.Where(p => p.Gender == g);
        }
        if (!string.IsNullOrWhiteSpace(countryId))
        {
            var c = countryId.Trim().ToUpperInvariant();
            q = q.Where(p => p.CountryId == c);
        }
        if (!string.IsNullOrWhiteSpace(ageGroup))
        {
            var a = ageGroup.Trim().ToLowerInvariant();
            q = q.Where(p => p.AgeGroup == a);
        }

        return await q.ToListAsync();
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var existing = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == id);
        if (existing == null) return false;
        _db.Profiles.Remove(existing);
        await _db.SaveChangesAsync();
        return true;
    }

    private static string ClassifyAgeGroup(int age)
    {
        if (age <= 12) return "child";
        if (age <= 19) return "teenager";
        if (age <= 59) return "adult";
        return "senior";
    }

    private static string ResolveCountryName(string countryId)
    {
        try
        {
            return new RegionInfo(countryId.ToUpperInvariant()).EnglishName;
        }
        catch (ArgumentException)
        {
            return countryId;
        }
    }
}
