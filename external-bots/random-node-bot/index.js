const http = require("node:http");
const crypto = require("node:crypto");

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

function randomInt(min, max) {
  return min + Math.floor(Math.random() * (max - min + 1));
}

function randomChoice(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
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
      await parseBody(req);
      return json(res, 200, {
        position: randomInt(6, 26),
        fromTop: Math.random() > 0.5,
      });
    }

    // POST /api/sessions/:sessionId/choose-negotiation-action
    const negMatch = path.match(
      /^\/api\/sessions\/([^/]+)\/choose-negotiation-action$/
    );
    if (method === "POST" && negMatch) {
      const body = await parseBody(req);
      return json(res, 200, randomChoice(body.validActions));
    }

    // POST /api/sessions/:sessionId/choose-card
    const cardMatch = path.match(
      /^\/api\/sessions\/([^/]+)\/choose-card$/
    );
    if (method === "POST" && cardMatch) {
      const body = await parseBody(req);
      return json(res, 200, randomChoice(body.validPlays));
    }

    // POST /api/sessions/:sessionId/notify/*
    const notifyMatch = path.match(
      /^\/api\/sessions\/([^/]+)\/notify\/.+$/
    );
    if (method === "POST" && notifyMatch) {
      await parseBody(req);
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
