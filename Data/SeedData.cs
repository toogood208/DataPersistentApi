using System.Text.Json;
using DataPersistentApi.Data;
using DataPersistentApi.Models;
using DataPersistentApi.Utils;
using Microsoft.EntityFrameworkCore;

public static class SeedData
{
    public static async Task EnsureSeededAsync(AppDBContext db, string jsonPath)
    {
        if (!File.Exists(jsonPath)) return;
        var txt = await File.ReadAllTextAsync(jsonPath);

        List<ProfileSeed> seeds = new List<ProfileSeed>();
        try
        {
            using var doc = JsonDocument.Parse(txt);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                seeds = JsonSerializer.Deserialize<List<ProfileSeed>>(txt) ?? new List<ProfileSeed>();
            }
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("profiles", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                seeds = JsonSerializer.Deserialize<List<ProfileSeed>>(arr.GetRawText()) ?? new List<ProfileSeed>();
            }
        }
        catch
        {
            // ignore malformed JSON
            return;
        }

        foreach (var s in seeds)
        {
            if (string.IsNullOrWhiteSpace(s.name)) continue;
            var exists = await db.Profiles.AnyAsync(p => p.Name.ToLower() == s.name.ToLower());
            if (exists) continue;
            var createdAt = s.created_at ?? DateTime.UtcNow;
            var p = new Profile {
                Id = UuidV7Generator.NewUuidV7(),
                Name = s.name,
                Gender = s.gender,
                GenderProbability = s.gender_probability,
                Age = s.age,
                AgeGroup = s.age_group,
                CountryId = s.country_id,
                CountryName = s.country_name,
                CountryProbability = s.country_probability,
                CreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)
            };
            db.Profiles.Add(p);
        }
        await db.SaveChangesAsync();
    }
}

public class ProfileSeed
{
    public string name { get; set; } = null!;
    public string gender { get; set; } = null!;
    public double gender_probability { get; set; }
    public int age { get; set; }
    public string age_group { get; set; } = null!;
    public string country_id { get; set; } = null!;
    public string country_name { get; set; } = null!;
    public double country_probability { get; set; }
    public DateTime? created_at { get; set; }
}
