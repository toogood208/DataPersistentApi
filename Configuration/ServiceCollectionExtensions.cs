using DataPersistentApi.Authorization;
using DataPersistentApi.Data;
using DataPersistentApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

namespace DataPersistentApi.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInsightaConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GitHubOAuthOptions>(configuration.GetSection(GitHubOAuthOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        return services;
    }

    public static IServiceCollection AddInsightaOpenApiAndCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.AddCors(options =>
        {
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
            options.AddDefaultPolicy(policy =>
            {
                if (allowedOrigins is { Length: > 0 })
                {
                    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
                    return;
                }

                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            });
        });

        return services;
    }

    public static IServiceCollection AddInsightaRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                await StartupResponseWriter.WriteErrorResponseAsync(
                    context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    "Rate limit exceeded",
                    ct);
            };

            options.AddPolicy(StartupConstants.AuthRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: RateLimitPartitioning.GetClientPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));

            options.AddPolicy(StartupConstants.ApiRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: RateLimitPartitioning.GetApiPartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    }));
        });

        return services;
    }

    public static IServiceCollection AddInsightaApplicationServices(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddScoped<ProfileService>();
        services.AddScoped<ProfilesQueryService>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<RefreshTokenService>();
        services.AddScoped<OAuthStateService>();
        services.AddScoped<GitHubOAuthService>();
        services.AddScoped<AuthCookieService>();
        services.AddScoped<IAuthorizationHandler, ActiveRoleHandler>();
        return services;
    }

    public static IServiceCollection AddInsightaParser(this IServiceCollection services, string contentRootPath)
    {
        services.AddSingleton(NlParserFactory.Create(contentRootPath));
        return services;
    }

    public static IServiceCollection AddInsightaDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(conn))
        {
            services.AddDbContext<AppDBContext>(opts => opts.UseSqlServer(conn));
        }
        else
        {
            services.AddDbContext<AppDBContext>(opts => opts.UseInMemoryDatabase("ProfilesDb"));
        }

        return services;
    }

    public static IServiceCollection AddInsightaAuthenticationAndAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
                options.TokenValidationParameters = JwtTokenService.CreateTokenValidationParameters(jwtOptions);
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (string.IsNullOrWhiteSpace(context.Token) &&
                            context.Request.Cookies.TryGetValue(AuthCookieService.AccessTokenCookieName, out var accessToken))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await StartupResponseWriter.WriteErrorResponseAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "Authentication is required");
                    },
                    OnForbidden = async context =>
                    {
                        await StartupResponseWriter.WriteErrorResponseAsync(
                            context.HttpContext,
                            StatusCodes.Status403Forbidden,
                            "Forbidden");
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(StartupConstants.AdminOnlyPolicy, policy =>
                policy.RequireAuthenticatedUser()
                    .AddRequirements(new ActiveRoleRequirement("admin")));

            options.AddPolicy(StartupConstants.ReadAccessPolicy, policy =>
                policy.RequireAuthenticatedUser()
                    .AddRequirements(new ActiveRoleRequirement("admin", "analyst")));
        });

        return services;
    }
}
