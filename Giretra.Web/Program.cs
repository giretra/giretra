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
        }

        app.UseCors();
        app.UseHttpsRedirection();
        app.UseAuthorization();

        app.MapControllers();
        app.MapHub<GameHub>("/hubs/game");

        app.Run();
    }
}
