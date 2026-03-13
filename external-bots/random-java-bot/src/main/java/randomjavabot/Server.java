// Server.java — HTTP boilerplate. Bot creators should not need to edit this file.
// All game logic lives in Bot.java.

package randomjavabot;

import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.PropertyNamingStrategies;
import com.fasterxml.jackson.databind.SerializationFeature;

import io.javalin.Javalin;
import io.javalin.http.Context;
import io.javalin.json.JavalinJackson;

public class Server {

    private static final ObjectMapper mapper = new ObjectMapper()
            .setPropertyNamingStrategy(PropertyNamingStrategies.LOWER_CAMEL_CASE)
            .setSerializationInclusion(JsonInclude.Include.NON_NULL)
            .configure(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES, false)
            .configure(DeserializationFeature.READ_ENUMS_USING_TO_STRING, true)
            .configure(SerializationFeature.WRITE_ENUMS_USING_TO_STRING, true);

    private static final ConcurrentHashMap<String, Bot> bots = new ConcurrentHashMap<>();

    public static void main(String[] args) {
        int port = Integer.parseInt(System.getenv().getOrDefault("PORT", "5063"));

        // ── Launcher watchdog ─────────────────────────────────────────
        // If LAUNCHER_PID is set, monitor the launcher process and exit if it dies.
        // This prevents orphan bot processes when the launcher crashes.

        String launcherPid = System.getenv("LAUNCHER_PID");
        if (launcherPid != null) {
            try {
                long pid = Long.parseLong(launcherPid);
                ProcessHandle.of(pid).ifPresent(handle -> {
                    handle.onExit().thenRun(() -> {
                        System.out.println("Launcher process exited, shutting down.");
                        System.exit(0);
                    });
                });
            } catch (NumberFormatException ignored) {
            }
        }

        Javalin app = Javalin.create(config -> {
            config.jsonMapper(new JavalinJackson(mapper, false));
            config.showJavalinBanner = false;
        });

        app.get("/health", ctx -> ctx.status(200));

        app.post("/api/sessions", ctx -> {
            SessionRequest req = ctx.bodyAsClass(SessionRequest.class);
            String sessionId = UUID.randomUUID().toString();
            bots.put(sessionId, new Bot(req.matchId(), req.seed()));
            ctx.status(201).json(mapper.createObjectNode().put("sessionId", sessionId));
        });

        app.delete("/api/sessions/{sessionId}", ctx -> {
            bots.remove(ctx.pathParam("sessionId"));
            ctx.status(204);
        });

        app.post("/api/sessions/{sessionId}/{action}", ctx -> {
            Bot bot = getBot(ctx);
            String action = ctx.pathParam("action");
            switch (action) {
                case "choose-cut" -> ctx.json(bot.chooseCut(ctx.bodyAsClass(ChooseCutContext.class)));
                case "choose-negotiation-action" -> ctx.json(bot.chooseNegotiationAction(ctx.bodyAsClass(ChooseNegotiationActionContext.class)));
                case "choose-card" -> ctx.json(bot.chooseCard(ctx.bodyAsClass(ChooseCardContext.class)));
                default -> ctx.status(404);
            }
        });

        app.post("/api/sessions/{sessionId}/notify/{event}", ctx -> {
            Bot bot = getBot(ctx);
            String event = ctx.pathParam("event");
            switch (event) {
                case "deal-started" -> bot.onDealStarted(ctx.bodyAsClass(DealStartedContext.class));
                case "negotiation-completed" -> bot.onNegotiationCompleted(ctx.bodyAsClass(NegotiationCompletedContext.class));
                case "card-played" -> bot.onCardPlayed(ctx.bodyAsClass(CardPlayedContext.class));
                case "trick-completed" -> bot.onTrickCompleted(ctx.bodyAsClass(TrickCompletedContext.class));
                case "deal-ended" -> bot.onDealEnded(ctx.bodyAsClass(DealEndedContext.class));
                case "match-ended" -> bot.onMatchEnded(ctx.bodyAsClass(MatchEndedContext.class));
            }
            ctx.status(200);
        });

        app.start("localhost", port);
        System.out.println("random-java-bot listening on port " + port);
    }

    private static Bot getBot(Context ctx) {
        Bot bot = bots.get(ctx.pathParam("sessionId"));
        if (bot == null) {
            ctx.status(404);
            throw new IllegalStateException("Session not found");
        }
        return bot;
    }
}
