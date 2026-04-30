using DataPersistentApi.Data;
using DataPersistentApi.Endpoints;
using DataPersistentApi.Middleware;
using Microsoft.EntityFrameworkCore;

namespace DataPersistentApi.Configuration;

public static class WebApplicationExtensions
{
    public static WebApplication UseInsightaPipeline(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseCors();
        app.UseHttpsRedirection();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        return app;
    }

    public static WebApplication MapInsightaEndpoints(this WebApplication app)
    {
        app.MapAuthEndpoints();
        app.MapProfileEndpoints();
        app.MapUserEndpoints();
        return app;
    }

    public static async Task<WebApplication> ApplyDatabaseStartupTasksAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }

        var envSeed = Environment.GetEnvironmentVariable("SEED");
        if (!string.IsNullOrEmpty(envSeed) && envSeed == "true")
        {
            await SeedData.EnsureSeededAsync(db, Path.Combine(app.Environment.ContentRootPath, "Data/seed/profiles-2026.json"));
        }

        return app;
    }
}
