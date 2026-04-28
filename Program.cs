using DataPersistentApi.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddInsightaConfiguration(builder.Configuration)
    .AddInsightaOpenApiAndCors(builder.Configuration)
    .AddInsightaRateLimiting()
    .AddInsightaApplicationServices()
    .AddInsightaParser(builder.Environment.ContentRootPath)
    .AddInsightaDatabase(builder.Configuration)
    .AddInsightaAuthenticationAndAuthorization(builder.Configuration);

var app = builder.Build();
app.UseInsightaPipeline();
app.MapInsightaEndpoints();
await app.ApplyDatabaseStartupTasksAsync();
app.Run();
