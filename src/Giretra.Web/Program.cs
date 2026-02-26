using Giretra.Model;
using Giretra.Web.Auth;
using Giretra.Web.Hubs;
using Giretra.Web.Middleware;
using Giretra.Web.Repositories;
using Giretra.Web.Services;
using Giretra.Web.Services.Elo;
using Giretra.Web.Services.Offline;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Giretra.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        LoadDotEnv();

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
            .Enrich.WithProperty("Host", Environment.MachineName)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}      {Message:lj}{NewLine}{Exception}");

        var seqUrl = Environment.GetEnvironmentVariable("SEQ_URL");
        if (!string.IsNullOrEmpty(seqUrl))
        {
            loggerConfig.WriteTo.Seq(
                seqUrl,
                apiKey: Environment.GetEnvironmentVariable("SEQ_API_KEY"),
                messageHandler: new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                    UseProxy = false,
                },
                restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information);
        }

        Log.Logger = loggerConfig.CreateLogger();

        try
        {
            var offline = args.Contains("--offline");

            Log.Information("Starting Giretra.Web{OfflineFlag}", offline ? " (OFFLINE mode)" : "");
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog();

            // Add services to the container.
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    // Use string enum serialization
                    options.JsonSerializerOptions.Converters.Add(
                        new System.Text.Json.Serialization.JsonStringEnumConverter());
                });

            builder.Services.AddOpenApi();

            // Add NSwag OpenAPI/Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddOpenApiDocument(config =>
            {
                config.Title = "Giretra API";
                config.Version = "v1";
                config.Description = "Malagasy Belote card game API";
                config.SchemaSettings = new NJsonSchema.Generation.SystemTextJsonSchemaGeneratorSettings
                {
                    SerializerOptions = new System.Text.Json.JsonSerializerOptions
                    {
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    }
                };
            });

            // Add SignalR with string enum serialization (matching REST API)
            builder.Services.AddSignalR()
                .AddJsonProtocol(options =>
                {
                    options.PayloadSerializerOptions.Converters.Add(
                        new System.Text.Json.Serialization.JsonStringEnumConverter());
                });

            // Add CORS for development
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });

            // Register repositories (singletons for in-memory storage)
            builder.Services.AddSingleton<IRoomRepository, InMemoryRoomRepository>();
            builder.Services.AddSingleton<IGameRepository, InMemoryGameRepository>();

            // Register services
            builder.Services.AddSingleton<AiPlayerRegistry>();
            builder.Services.AddSingleton<INotificationService, NotificationService>();
            builder.Services.AddSingleton<IGameService, GameService>();
            builder.Services.AddSingleton<IRoomService, RoomService>();

            if (offline)
            {
                // Offline auth: simple username-based scheme
                builder.Services.AddAuthentication("Offline")
                    .AddScheme<AuthenticationSchemeOptions, OfflineAuthenticationHandler>("Offline", null);
                builder.Services.AddAuthorization();

                // Offline service stubs (no DB needed)
                builder.Services.AddOfflineServices();
            }
            else
            {
                // Authentication
                var keycloakSection = builder.Configuration.GetSection("Keycloak");
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.Authority = keycloakSection["Authority"];
                        options.Audience = keycloakSection["Audience"];
                        options.RequireHttpsMetadata = keycloakSection.GetValue<bool>("RequireHttpsMetadata");
                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                // Extract access_token from query string for SignalR
                                var accessToken = context.Request.Query["access_token"];
                                var path = context.HttpContext.Request.Path;
                                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                                {
                                    context.Token = accessToken;
                                }
                                return Task.CompletedTask;
                            }
                        };
                    });
                builder.Services.AddAuthorization();
                builder.Services.AddTransient<IClaimsTransformation, KeycloakClaimsTransformation>();

                // Database
                builder.Services.AddGiretraDb();

                // User sync
                builder.Services.AddScoped<IUserSyncService, UserSyncService>();

                // Persistence
                builder.Services.AddScoped<IMatchPersistenceService, MatchPersistenceService>();

                // Elo
                builder.Services.AddSingleton<EloCalculationService>();
                builder.Services.AddScoped<IEloService, EloService>();

                // Settings
                builder.Services.AddScoped<IProfileService, ProfileService>();
                builder.Services.AddScoped<IFriendService, FriendService>();
                builder.Services.AddScoped<IBlockService, BlockService>();
                builder.Services.AddScoped<IMatchHistoryService, MatchHistoryService>();
                builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
            }

            var app = builder.Build();

            // Auth config endpoint (tells frontend which auth mode to use)
            app.MapGet("/api/auth/config", () => offline
                ? Results.Ok(new { mode = "offline" })
                : Results.Ok(new { mode = "keycloak" }))
                .AllowAnonymous();

            var aiRegistry = app.Services.GetRequiredService<AiPlayerRegistry>();
            var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

            if (offline)
            {
                // Initialize AI registry via factory discovery (no database)
                await aiRegistry.InitializeOfflineAsync(lifetime.ApplicationStopping);
                Log.Information("Running in OFFLINE mode (no database, no Keycloak)");
            }
            else
            {
                // Auto-create database schema and seed bot Player rows
                using (var scope = app.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<GiretraDbContext>();
                    await db.Database.EnsureCreatedAsync();

                    // Ensure each Bot has a corresponding Player row with synced rating
                    var allBots = await db.Bots
                        .Include(b => b.Player)
                        .ToListAsync();

                    foreach (var bot in allBots)
                    {
                        if (bot.Player == null)
                        {
                            db.Players.Add(new Model.Entities.Player
                            {
                                PlayerType = Model.Enums.PlayerType.Bot,
                                BotId = bot.Id,
                                EloRating = bot.Rating,
                                EloIsPublic = true,
                                CreatedAt = DateTimeOffset.UtcNow,
                                UpdatedAt = DateTimeOffset.UtcNow
                            });
                        }
                        else if (bot.Player.EloRating != bot.Rating)
                        {
                            // Sync Player.EloRating with Bot.Rating
                            bot.Player.EloRating = bot.Rating;
                            bot.Player.UpdatedAt = DateTimeOffset.UtcNow;
                        }
                    }

                    if (db.ChangeTracker.HasChanges())
                        await db.SaveChangesAsync();
                }

                // Load active bots from database into the AI registry
                await aiRegistry.InitializeAsync(lifetime.ApplicationStopping);
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();

                // NSwag middleware
                app.UseOpenApi();
                app.UseSwaggerUi(config =>
                {
                    config.DocumentTitle = "Giretra API";
                    config.Path = "/swagger";
                    config.DocumentPath = "/swagger/{documentName}/swagger.json";
                });
            }

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseSerilogRequestLogging(options =>
            {
                options.GetLevel = (httpContext, _, ex) =>
                {
                    if (ex != null || httpContext.Response.StatusCode >= 500)
                        return Serilog.Events.LogEventLevel.Error;

                    return httpContext.Response.StatusCode is 401 or 404
                        ? Serilog.Events.LogEventLevel.Verbose
                        : Serilog.Events.LogEventLevel.Information;
                };
            });

            app.UseCors();
            app.UseAuthentication();
            app.UseMiddleware<UserSyncMiddleware>();
            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<GameHub>("/hubs/game");
            app.MapFallbackToFile("index.html");

            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    private static void LoadDotEnv()
    {
        // Walk up from cwd to find .env (handles launch from subdirectory)
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        string? path = null;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate)) { path = candidate; break; }
            dir = dir.Parent;
        }
        if (path is null)
            return;

        foreach (var (lineNumber, line) in File.ReadAllLines(path).Select((l, i) => (i + 1, l)))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
                throw new InvalidOperationException(
                    $".env parse error at line {lineNumber}: missing '=' separator in \"{line}\"");

            var key = line[..separatorIndex].Trim();
            if (key.Length == 0)
                throw new InvalidOperationException(
                    $".env parse error at line {lineNumber}: empty variable name");

            var value = line[(separatorIndex + 1)..].Trim();

            // Strip matching quotes
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') ||
                 (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
