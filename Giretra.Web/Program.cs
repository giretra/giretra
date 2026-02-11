using Giretra.Model;
using Giretra.Web.Hubs;
using Giretra.Web.Repositories;
using Giretra.Web.Services;
using Serilog;

namespace Giretra.Web;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}      {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Log.Information("Starting Giretra.Web");
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

            // Database
            builder.Services.AddGiretraDb();

            // Persistence
            builder.Services.AddScoped<IMatchPersistenceService, MatchPersistenceService>();

            var app = builder.Build();

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

            app.UseCors();
            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<GameHub>("/hubs/game");

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
