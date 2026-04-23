using Microsoft.EntityFrameworkCore;
using DataPersistentApi.Data;
using DataPersistentApi.Models;

namespace DataPersistentApi.Services;

public class ProfilesQueryService
{
    private readonly AppDBContext _db;
    public ProfilesQueryService(AppDBContext db) => _db = db;

    public async Task<(int total, List<object> items)> QueryAsync(QueryOptions opts, CancellationToken ct = default)
    {
        opts.Page = Math.Max(1, opts.Page);
        opts.Limit = Math.Clamp(opts.Limit, 1, 50);

        IQueryable<Profile> q = _db.Profiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(opts.Gender))
            q = q.Where(p => p.Gender == opts.Gender);

        if (!string.IsNullOrWhiteSpace(opts.AgeGroup))
            q = q.Where(p => p.AgeGroup == opts.AgeGroup);

        if (!string.IsNullOrWhiteSpace(opts.CountryId))
            q = q.Where(p => p.CountryId == opts.CountryId);

        if (opts.MinAge.HasValue) q = q.Where(p => p.Age >= opts.MinAge.Value);
        if (opts.MaxAge.HasValue) q = q.Where(p => p.Age <= opts.MaxAge.Value);
        if (opts.MinGenderProbability.HasValue) q = q.Where(p => p.GenderProbability >= opts.MinGenderProbability.Value);
        if (opts.MinCountryProbability.HasValue) q = q.Where(p => p.CountryProbability >= opts.MinCountryProbability.Value);

        var total = await q.CountAsync(ct);

        var order = (opts.Order ?? "desc").ToLower();
        q = (opts.SortBy ?? "created_at").ToLower() switch
        {
            "age" => order == "asc" ? q.OrderBy(p => p.Age) : q.OrderByDescending(p => p.Age),
            "gender_probability" => order == "asc" ? q.OrderBy(p => p.GenderProbability) : q.OrderByDescending(p => p.GenderProbability),
            _ => order == "asc" ? q.OrderBy(p => p.CreatedAt) : q.OrderByDescending(p => p.CreatedAt)
        };

        var skip = (opts.Page - 1) * opts.Limit;
        var items = await q
            .Skip(skip)
            .Take(opts.Limit)
            .Select(p => new {
                id = p.Id,
                name = p.Name,
                gender = p.Gender,
                gender_probability = p.GenderProbability,
                age = p.Age,
                age_group = p.AgeGroup,
                country_id = p.CountryId,
                country_name = p.CountryName,
                country_probability = p.CountryProbability,
                created_at = p.CreatedAt
            })
            .ToListAsync(ct);

        return (total, items.Cast<object>().ToList());
    }
}
