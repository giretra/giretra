// server.ts — HTTP boilerplate. Bot creators should not need to edit this file.
// All game logic lives in bot.ts.

import * as http from "node:http";
import * as crypto from "node:crypto";
import { Bot } from "./bot";
import {
  SessionRequest,
  ChooseCutContext,
  ChooseNegotiationActionContext,
  ChooseCardContext,
  DealStartedContext,
  CardPlayedContext,
  TrickCompletedContext,
  DealEndedContext,
  MatchEndedContext,
} from "./types";

const PORT = parseInt(process.env.PORT ?? "", 10) || 5060;

const bots = new Map<string, Bot>();

function parseBody(req: http.IncomingMessage): Promise<any> {
  return new Promise((resolve, reject) => {
    const chunks: Buffer[] = [];
    req.on("data", (chunk: Buffer) => chunks.push(chunk));
    req.on("end", () => {
      const raw = Buffer.concat(chunks).toString();
      resolve(raw ? JSON.parse(raw) : null);
    });
    req.on("error", reject);
  });
}

function json(res: http.ServerResponse, status: number, data: unknown): void {
  const body = JSON.stringify(data);
  res.writeHead(status, {
    "Content-Type": "application/json",
    "Content-Length": Buffer.byteLength(body),
  });
  res.end(body);
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url!, `http://localhost:${PORT}`);
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
      const body = (await parseBody(req)) as SessionRequest;
      const sessionId = crypto.randomUUID();
      bots.set(sessionId, new Bot(body.matchId));
      return json(res, 201, { sessionId });
    }

    // DELETE /api/sessions/:sessionId
    const deleteMatch = path.match(/^\/api\/sessions\/([^/]+)$/);
    if (method === "DELETE" && deleteMatch) {
      bots.delete(deleteMatch[1]);
      res.writeHead(204);
      return res.end();
    }

    // POST /api/sessions/:sessionId/choose-cut
    const cutMatch = path.match(
      /^\/api\/sessions\/([^/]+)\/choose-cut$/
    );
    if (method === "POST" && cutMatch) {
      const body = (await parseBody(req)) as ChooseCutContext;
      const result = bots.get(cutMatch[1])!.chooseCut(body);
      return json(res, 200, result);
    }

    // POST /api/sessions/:sessionId/choose-negotiation-action
    const negMatch = path.match(
      /^\/api\/sessions\/([^/]+)\/choose-negotiation-action$/
    );
    if (method === "POST" && negMatch) {
      const body = (await parseBody(req)) as ChooseNegotiationActionContext;
      const result = bots.get(negMatch[1])!.chooseNegotiationAction(body);
      return json(res, 200, result);
    }

    // POST /api/sessions/:sessionId/choose-card
    const cardMatch = path.match(
      /^\/api\/sessions\/([^/]+)\/choose-card$/
    );
    if (method === "POST" && cardMatch) {
      const body = (await parseBody(req)) as ChooseCardContext;
      const result = bots.get(cardMatch[1])!.chooseCard(body);
      return json(res, 200, result);
    }

    // POST /api/sessions/:sessionId/notify/*
    const notifyMatch = path.match(
      /^\/api\/sessions\/([^/]+)\/notify\/(.+)$/
    );
    if (method === "POST" && notifyMatch) {
      const body = await parseBody(req);
      const bot = bots.get(notifyMatch[1])!;

      switch (notifyMatch[2]) {
        case "deal-started":
          bot.onDealStarted(body as DealStartedContext);
          break;
        case "card-played":
          bot.onCardPlayed(body as CardPlayedContext);
          break;
        case "trick-completed":
          bot.onTrickCompleted(body as TrickCompletedContext);
          break;
        case "deal-ended":
          bot.onDealEnded(body as DealEndedContext);
          break;
        case "match-ended":
          bot.onMatchEnded(body as MatchEndedContext);
          break;
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
