using DataPersistentApi.Data;
using DataPersistentApi.Endpoints;
using DataPersistentApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// CORS - allow all origins (grading requires Access-Control-Allow-Origin: *)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// HttpClient for external API calls
builder.Services.AddHttpClient();

// Register ProfileService
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<ProfilesQueryService>();

// Build a country map from seed data (if available) to improve NL parsing
var countryMap = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
try
{
    var seedPath = Path.Combine(builder.Environment.ContentRootPath, "Data/seed/profiles-2026.json");
    if (File.Exists(seedPath))
    {
        var txt = File.ReadAllText(seedPath);
        using var doc = System.Text.Json.JsonDocument.Parse(txt);
        var root = doc.RootElement;
        System.Text.Json.JsonElement arr;
        if (root.ValueKind == System.Text.Json.JsonValueKind.Object && root.TryGetProperty("profiles", out arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var el in arr.EnumerateArray())
            {
                if (el.TryGetProperty("country_name", out var cn) && el.TryGetProperty("country_id", out var ci))
                {
                    var name = cn.GetString();
                    var id = ci.GetString();
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(id))
                    {
                        var key = name.ToLowerInvariant();
                        if (!countryMap.ContainsKey(key)) countryMap[key] = id.ToUpperInvariant();
                    }
                }
            }
        }
    }
}
catch
{
    // ignore any errors reading seed file
}

builder.Services.AddSingleton(new NlParser(countryMap));

// Configure DbContext: prefer connection string "DefaultConnection", fallback to InMemory for simplicity
var conn = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(conn))
{
    builder.Services.AddDbContext<AppDBContext>(opts => opts.UseSqlServer(conn));
}
else
{
    builder.Services.AddDbContext<AppDBContext>(opts => opts.UseInMemoryDatabase("ProfilesDb"));
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();

// Map profile endpoints from separate endpoint definitions
app.MapProfileEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
    // seed (only when flag present to avoid production accidental seed)
    var envSeed = Environment.GetEnvironmentVariable("SEED");
    if (!string.IsNullOrEmpty(envSeed) && envSeed == "true")
    {
        await SeedData.EnsureSeededAsync(db, Path.Combine(app.Environment.ContentRootPath, "Data/seed/profiles-2026.json"));
    }
}           

app.Run();


