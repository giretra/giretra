using Giretra.Web.Hubs;
using Giretra.Web.Repositories;
using Giretra.Web.Services;

namespace Giretra.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

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
            config.SerializerSettings = new NJsonSchema.Generation.SystemTextJsonSchemaGeneratorSettings
            {
                SerializerOptions = new System.Text.Json.JsonSerializerOptions
                {
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                }
            };
        });

        // Add SignalR
        builder.Services.AddSignalR();

        // Add CORS for development
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        // Register repositories (singletons for in-memory storage)
        builder.Services.AddSingleton<IRoomRepository, InMemoryRoomRepository>();
        builder.Services.AddSingleton<IGameRepository, InMemoryGameRepository>();

        // Register services
        builder.Services.AddSingleton<INotificationService, NotificationService>();
        builder.Services.AddSingleton<IGameService, GameService>();
        builder.Services.AddSingleton<IRoomService, RoomService>();

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
        app.UseHttpsRedirection();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<GameHub>("/hubs/game");

        app.Run();
    }
}
