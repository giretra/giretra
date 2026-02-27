// Server.java — HTTP boilerplate. Bot creators should not need to edit this file.
// All game logic lives in Bot.java.

package randomjavabot;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.PropertyNamingStrategies;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;

public class Server {

    private static final ObjectMapper mapper = new ObjectMapper()
            .setPropertyNamingStrategy(PropertyNamingStrategies.LOWER_CAMEL_CASE)
            .setSerializationInclusion(JsonInclude.Include.NON_NULL)
            .configure(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES, false)
            .configure(DeserializationFeature.READ_ENUMS_USING_TO_STRING, true)
            .configure(SerializationFeature.WRITE_ENUMS_USING_TO_STRING, true);

    private static final ConcurrentHashMap<String, Bot> bots = new ConcurrentHashMap<>();

    public static void main(String[] args) throws IOException {
        int port = Integer.parseInt(System.getenv().getOrDefault("PORT", "5063"));
        HttpServer server = HttpServer.create(new InetSocketAddress("localhost", port), 0);

        server.createContext("/health", exchange -> {
            if (!"GET".equals(exchange.getRequestMethod())) {
                exchange.sendResponseHeaders(405, -1);
                exchange.close();
                return;
            }
            exchange.sendResponseHeaders(200, -1);
            exchange.close();
        });

        server.createContext("/api/sessions", exchange -> {
            try {
                handleSessions(exchange);
            } catch (Exception e) {
                e.printStackTrace();
                exchange.sendResponseHeaders(500, -1);
                exchange.close();
            }
        });

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

        server.setExecutor(null);
        server.start();
        System.out.println("random-java-bot listening on port " + port);
    }

    private static void handleSessions(HttpExchange exchange) throws IOException {
        String path = exchange.getRequestURI().getPath();
        String method = exchange.getRequestMethod();

        // POST /api/sessions
        if ("POST".equals(method) && path.equals("/api/sessions")) {
            SessionRequest req = readBody(exchange, SessionRequest.class);
            String sessionId = UUID.randomUUID().toString();
            bots.put(sessionId, new Bot(req.matchId()));
            sendJson(exchange, 201, mapper.createObjectNode().put("sessionId", sessionId));
            return;
        }

        // Extract session ID from path: /api/sessions/{sessionId}[/...]
        String[] segments = path.substring("/api/sessions/".length()).split("/", 2);
        String sessionId = segments[0];

        // DELETE /api/sessions/{sessionId}
        if ("DELETE".equals(method) && segments.length == 1) {
            bots.remove(sessionId);
            exchange.sendResponseHeaders(204, -1);
            exchange.close();
            return;
        }

        if (!"POST".equals(method) || segments.length < 2) {
            exchange.sendResponseHeaders(404, -1);
            exchange.close();
            return;
        }

        Bot bot = bots.get(sessionId);
        if (bot == null) {
            exchange.sendResponseHeaders(404, -1);
            exchange.close();
            return;
        }

        String action = segments[1];

        switch (action) {
            case "choose-cut" -> {
                ChooseCutContext ctx = readBody(exchange, ChooseCutContext.class);
                sendJson(exchange, 200, bot.chooseCut(ctx));
            }
            case "choose-negotiation-action" -> {
                ChooseNegotiationActionContext ctx = readBody(exchange, ChooseNegotiationActionContext.class);
                sendJson(exchange, 200, bot.chooseNegotiationAction(ctx));
            }
            case "choose-card" -> {
                ChooseCardContext ctx = readBody(exchange, ChooseCardContext.class);
                sendJson(exchange, 200, bot.chooseCard(ctx));
            }
            default -> {
                // notify/{event}
                if (action.startsWith("notify/")) {
                    String event = action.substring("notify/".length());
                    handleNotify(exchange, bot, event);
                } else {
                    exchange.sendResponseHeaders(404, -1);
                    exchange.close();
                }
            }
        }
    }

    private static void handleNotify(HttpExchange exchange, Bot bot, String event) throws IOException {
        switch (event) {
            case "deal-started" -> bot.onDealStarted(readBody(exchange, DealStartedContext.class));
            case "card-played" -> bot.onCardPlayed(readBody(exchange, CardPlayedContext.class));
            case "trick-completed" -> bot.onTrickCompleted(readBody(exchange, TrickCompletedContext.class));
            case "deal-ended" -> bot.onDealEnded(readBody(exchange, DealEndedContext.class));
            case "match-ended" -> bot.onMatchEnded(readBody(exchange, MatchEndedContext.class));
        }
        exchange.sendResponseHeaders(200, -1);
        exchange.close();
    }

    private static <T> T readBody(HttpExchange exchange, Class<T> type) throws IOException {
        try (InputStream is = exchange.getRequestBody()) {
            return mapper.readValue(is, type);
        }
    }

    private static void sendJson(HttpExchange exchange, int status, Object body) throws IOException {
        byte[] bytes = mapper.writeValueAsBytes(body);
        exchange.getResponseHeaders().set("Content-Type", "application/json");
        exchange.sendResponseHeaders(status, bytes.length);
        try (OutputStream os = exchange.getResponseBody()) {
            os.write(bytes);
        }
    }
}
