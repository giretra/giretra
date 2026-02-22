// server.js — HTTP boilerplate. Bot creators should not need to edit this file.
// All game logic lives in bot.js.

const http = require("node:http");
const crypto = require("node:crypto");
const bot = require("./dist/bot");

const PORT = parseInt(process.env.PORT, 10) || 5060;

const sessions = new Map();

function parseBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    req.on("data", (chunk) => chunks.push(chunk));
    req.on("end", () => {
      const raw = Buffer.concat(chunks).toString();
      resolve(raw ? JSON.parse(raw) : null);
    });
    req.on("error", reject);
  });
}

function json(res, status, data) {
  const body = JSON.stringify(data);
  res.writeHead(status, {
    "Content-Type": "application/json",
    "Content-Length": Buffer.byteLength(body),
  });
  res.end(body);
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://localhost:${PORT}`);
  const path = url.pathname;
  const method = req.method;

  try {
    // GET /health
    if (method === "GET" && path === "/health") {
      res.writeHead(200);
      return res.end();
    }

    // POST /api/sessions
    if (method === "POST" && path === "/api/sessions") {
      const body = await parseBody(req);
      const sessionId = crypto.randomUUID();
      sessions.set(sessionId, {
        position: body.position,
        matchId: body.matchId,
      });
      return json(res, 201, { sessionId });
    }

    // DELETE /api/sessions/:sessionId
    const deleteMatch = path.match(/^\/api\/sessions\/([^/]+)$/);
    if (method === "DELETE" && deleteMatch) {
      sessions.delete(deleteMatch[1]);
      res.writeHead(204);
      return res.end();
    }

    // POST /api/sessions/:sessionId/choose-cut
    const cutMatch = path.match(
      /^\/api\/sessions\/([^/]+)\/choose-cut$/
    );
    if (method === "POST" && cutMatch) {
      const body = await parseBody(req);
      const session = sessions.get(cutMatch[1]);
      const result = bot.chooseCut({
        deckSize: body.deckSize,
        matchState: body.matchState,
        session,
      });
      return json(res, 200, result);
    }

    // POST /api/sessions/:sessionId/choose-negotiation-action
    const negMatch = path.match(
      /^\/api\/sessions\/([^/]+)\/choose-negotiation-action$/
    );
    if (method === "POST" && negMatch) {
      const body = await parseBody(req);
      const session = sessions.get(negMatch[1]);
      const result = bot.chooseNegotiationAction({
        hand: body.hand,
        negotiationState: body.negotiationState,
        matchState: body.matchState,
        validActions: body.validActions,
        session,
      });
      return json(res, 200, result);
    }

    // POST /api/sessions/:sessionId/choose-card
    const cardMatch = path.match(
      /^\/api\/sessions\/([^/]+)\/choose-card$/
    );
    if (method === "POST" && cardMatch) {
      const body = await parseBody(req);
      const session = sessions.get(cardMatch[1]);
      const result = bot.chooseCard({
        hand: body.hand,
        handState: body.handState,
        matchState: body.matchState,
        validPlays: body.validPlays,
        session,
      });
      return json(res, 200, result);
    }

    // POST /api/sessions/:sessionId/notify/*
    const notifyMatch = path.match(
      /^\/api\/sessions\/([^/]+)\/notify\/(.+)$/
    );
    if (method === "POST" && notifyMatch) {
      const body = await parseBody(req);
      const session = sessions.get(notifyMatch[1]);
      const eventName = notifyMatch[2];

      const handlers = {
        "deal-started": bot.onDealStarted,
        "card-played": bot.onCardPlayed,
        "trick-completed": bot.onTrickCompleted,
        "deal-ended": bot.onDealEnded,
        "match-ended": bot.onMatchEnded,
      };

      const handler = handlers[eventName];
      if (handler) {
        handler({ ...body, session });
      }

      res.writeHead(200);
      return res.end();
    }

    // 404 for anything else
    res.writeHead(404);
    res.end();
  } catch (err) {
    console.error("Error handling request:", err);
    res.writeHead(500);
    res.end();
  }
});

server.listen(PORT, () => {
  console.log(`random-node-bot listening on port ${PORT}`);
});
