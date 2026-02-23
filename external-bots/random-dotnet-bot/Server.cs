// Server.cs — HTTP boilerplate. Bot creators should not need to edit this file.
// All game logic lives in Bot.cs.

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using RandomDotnetBot;

var port = int.Parse(Environment.GetEnvironmentVariable("PORT") ?? "5062");

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
builder.Logging.ClearProviders();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new LenientBoolConverter());
});

var app = builder.Build();
var sessions = new ConcurrentDictionary<string, Session>();

// ── Health ───────────────────────────────────────────────────────────

app.MapGet("/health", () => Results.Ok());

// ── Sessions ─────────────────────────────────────────────────────────

app.MapPost("/api/sessions", async (HttpRequest req) =>
{
    var body = await req.ReadFromJsonAsync<Session>();
    var sessionId = Guid.NewGuid().ToString();
    sessions[sessionId] = body!;
    return Results.Created($"/api/sessions/{sessionId}", new { sessionId });
});

app.MapDelete("/api/sessions/{sessionId}", (string sessionId) =>
{
    sessions.TryRemove(sessionId, out _);
    return Results.NoContent();
});

// ── Decisions ────────────────────────────────────────────────────────

app.MapPost("/api/sessions/{sessionId}/choose-cut",
    async (string sessionId, HttpRequest req) =>
    {
        var ctx = await req.ReadFromJsonAsync<ChooseCutContext>();
        ctx!.Session = sessions.GetValueOrDefault(sessionId);
        return Results.Ok(Bot.ChooseCut(ctx));
    });

app.MapPost("/api/sessions/{sessionId}/choose-negotiation-action",
    async (string sessionId, HttpRequest req) =>
    {
        var ctx = await req.ReadFromJsonAsync<ChooseNegotiationActionContext>();
        ctx!.Session = sessions.GetValueOrDefault(sessionId);
        return Results.Ok(Bot.ChooseNegotiationAction(ctx));
    });

app.MapPost("/api/sessions/{sessionId}/choose-card",
    async (string sessionId, HttpRequest req) =>
    {
        var ctx = await req.ReadFromJsonAsync<ChooseCardContext>();
        ctx!.Session = sessions.GetValueOrDefault(sessionId);
        return Results.Ok(Bot.ChooseCard(ctx));
    });

// ── Notifications ────────────────────────────────────────────────────

app.MapPost("/api/sessions/{sessionId}/notify/{eventName}",
    async (string sessionId, string eventName, HttpRequest req) =>
    {
        var session = sessions.GetValueOrDefault(sessionId);

        switch (eventName)
        {
            case "deal-started":
            {
                var ctx = await req.ReadFromJsonAsync<DealStartedContext>();
                ctx!.Session = session;
                Bot.OnDealStarted(ctx);
                break;
            }
            case "card-played":
            {
                var ctx = await req.ReadFromJsonAsync<CardPlayedContext>();
                ctx!.Session = session;
                Bot.OnCardPlayed(ctx);
                break;
            }
            case "trick-completed":
            {
                var ctx = await req.ReadFromJsonAsync<TrickCompletedContext>();
                ctx!.Session = session;
                Bot.OnTrickCompleted(ctx);
                break;
            }
            case "deal-ended":
            {
                var ctx = await req.ReadFromJsonAsync<DealEndedContext>();
                ctx!.Session = session;
                Bot.OnDealEnded(ctx);
                break;
            }
            case "match-ended":
            {
                var ctx = await req.ReadFromJsonAsync<MatchEndedContext>();
                ctx!.Session = session;
                Bot.OnMatchEnded(ctx);
                break;
            }
        }

        return Results.Ok();
    });

Console.WriteLine($"random-dotnet-bot listening on port {port}");
app.Run();

sealed class LenientBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => reader.GetInt32() != 0,
            _ => throw new JsonException($"Cannot convert {reader.TokenType} to Boolean"),
        };

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteBooleanValue(value);
}
