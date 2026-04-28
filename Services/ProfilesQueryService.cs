using Microsoft.EntityFrameworkCore;
using DataPersistentApi.Data;
using DataPersistentApi.Models;

namespace DataPersistentApi.Services;

public class ProfilesQueryService
{
    private readonly AppDBContext _db;
    public ProfilesQueryService(AppDBContext db) => _db = db;

    public async Task<(int total, List<ProfileListItemDto> items)> QueryAsync(QueryOptions opts, CancellationToken ct = default)
    {
        opts.Page = Math.Max(1, opts.Page);
        opts.Limit = Math.Clamp(opts.Limit, 1, 50);

        var q = ApplySorting(BuildFilteredQuery(opts), opts);
        var total = await q.CountAsync(ct);

        var skip = (opts.Page - 1) * opts.Limit;
        var items = await q
            .Skip(skip)
            .Take(opts.Limit)
            .Select(ProjectListItem())
            .ToListAsync(ct);

        return (total, items);
    }

    public async Task<List<ProfileListItemDto>> QueryAllAsync(QueryOptions opts, CancellationToken ct = default)
    {
        return await ApplySorting(BuildFilteredQuery(opts), opts)
            .Select(ProjectListItem())
            .ToListAsync(ct);
    }

    private IQueryable<Profile> BuildFilteredQuery(QueryOptions opts)
    {
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

        return q;
    }

    private static IQueryable<Profile> ApplySorting(IQueryable<Profile> q, QueryOptions opts)
    {
        var order = (opts.Order ?? "desc").ToLowerInvariant();
        return (opts.SortBy ?? "created_at").ToLowerInvariant() switch
        {
            "age" => order == "asc" ? q.OrderBy(p => p.Age) : q.OrderByDescending(p => p.Age),
            "gender_probability" => order == "asc" ? q.OrderBy(p => p.GenderProbability) : q.OrderByDescending(p => p.GenderProbability),
            _ => order == "asc" ? q.OrderBy(p => p.CreatedAt) : q.OrderByDescending(p => p.CreatedAt)
        };
    }

    private static System.Linq.Expressions.Expression<Func<Profile, ProfileListItemDto>> ProjectListItem()
    {
        return p => new ProfileListItemDto(
            p.Id,
            p.Name,
            p.Gender,
            p.GenderProbability,
            p.Age,
            p.AgeGroup,
            p.CountryId,
            p.CountryName,
            p.CountryProbability,
            p.CreatedAt);
    }
}
