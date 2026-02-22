# server.py — HTTP boilerplate. Bot creators should not need to edit this file.
# All game logic lives in bot.py.
# Requires Python 3.8+.

import json
import os
import re
import uuid
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler

import bot

PORT = int(os.environ.get("PORT", 5061))

sessions: dict = {}


class BotHandler(BaseHTTPRequestHandler):
    # Use HTTP/1.1 to support keep-alive and chunked transfer encoding.
    protocol_version = "HTTP/1.1"

    def do_GET(self):
        if self.path == "/health":
            self.send_response(200)
            self.send_header("Content-Length", "0")
            self.end_headers()
            return
        self.send_response(404)
        self.send_header("Content-Length", "0")
        self.end_headers()

    def do_POST(self):
        body = self._read_body()

        # POST /api/sessions
        if self.path == "/api/sessions":
            session_id = str(uuid.uuid4())
            sessions[session_id] = {
                "position": body["position"],
                "matchId": body["matchId"],
            }
            self._json(201, {"sessionId": session_id})
            return

        # POST /api/sessions/:id/choose-cut
        m = re.match(r"^/api/sessions/([^/]+)/choose-cut$", self.path)
        if m:
            session = sessions.get(m.group(1))
            result = bot.choose_cut({
                "deckSize": body["deckSize"],
                "matchState": body["matchState"],
                "session": session,
            })
            self._json(200, result)
            return

        # POST /api/sessions/:id/choose-negotiation-action
        m = re.match(r"^/api/sessions/([^/]+)/choose-negotiation-action$", self.path)
        if m:
            session = sessions.get(m.group(1))
            result = bot.choose_negotiation_action({
                "hand": body["hand"],
                "negotiationState": body["negotiationState"],
                "matchState": body["matchState"],
                "validActions": body["validActions"],
                "session": session,
            })
            self._json(200, result)
            return

        # POST /api/sessions/:id/choose-card
        m = re.match(r"^/api/sessions/([^/]+)/choose-card$", self.path)
        if m:
            session = sessions.get(m.group(1))
            result = bot.choose_card({
                "hand": body["hand"],
                "handState": body["handState"],
                "matchState": body["matchState"],
                "validPlays": body["validPlays"],
                "session": session,
            })
            self._json(200, result)
            return

        # POST /api/sessions/:id/notify/:event
        m = re.match(r"^/api/sessions/([^/]+)/notify/(.+)$", self.path)
        if m:
            session = sessions.get(m.group(1))
            event_name = m.group(2)
            handlers = {
                "deal-started": getattr(bot, "on_deal_started", None),
                "card-played": getattr(bot, "on_card_played", None),
                "trick-completed": getattr(bot, "on_trick_completed", None),
                "deal-ended": getattr(bot, "on_deal_ended", None),
                "match-ended": getattr(bot, "on_match_ended", None),
            }
            handler = handlers.get(event_name)
            if handler:
                handler({**body, "session": session})
            self.send_response(200)
            self.send_header("Content-Length", "0")
            self.end_headers()
            return

        self.send_response(404)
        self.send_header("Content-Length", "0")
        self.end_headers()

    def do_DELETE(self):
        m = re.match(r"^/api/sessions/([^/]+)$", self.path)
        if m:
            sessions.pop(m.group(1), None)
            self.send_response(204)
            self.send_header("Content-Length", "0")
            self.end_headers()
            return
        self.send_response(404)
        self.send_header("Content-Length", "0")
        self.end_headers()

    # ── helpers ──────────────────────────────────────────────────────

    def _read_body(self):
        length = int(self.headers.get("Content-Length", 0))
        if length == 0:
            return None
        return json.loads(self.rfile.read(length))

    def _json(self, status, data):
        payload = json.dumps(data).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def log_message(self, format, *args):
        pass  # suppress per-request logging


if __name__ == "__main__":
    server = ThreadingHTTPServer(("", PORT), BotHandler)
    print(f"random-python-bot listening on port {PORT}")
    server.serve_forever()
