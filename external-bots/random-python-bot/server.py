# server.py — HTTP boilerplate. Bot creators should not need to edit this file.
# All game logic lives in bot.py.
# Requires Python 3.8+ and aiohttp (`pip install aiohttp`).

import os
import uuid

from aiohttp import web

from bot import Bot

PORT = int(os.environ.get("PORT", 5061))

bots: dict[str, Bot] = {}


async def health(request: web.Request) -> web.Response:
    return web.Response(status=200)


async def create_session(request: web.Request) -> web.Response:
    body = await request.json()
    session_id = str(uuid.uuid4())
    bots[session_id] = Bot(body["matchId"])
    return web.json_response({"sessionId": session_id}, status=201)


async def delete_session(request: web.Request) -> web.Response:
    session_id = request.match_info["session_id"]
    bots.pop(session_id, None)
    return web.Response(status=204)


async def choose_cut(request: web.Request) -> web.Response:
    session_id = request.match_info["session_id"]
    body = await request.json()
    result = bots[session_id].choose_cut(body)
    return web.json_response(result)


async def choose_negotiation_action(request: web.Request) -> web.Response:
    session_id = request.match_info["session_id"]
    body = await request.json()
    result = bots[session_id].choose_negotiation_action(body)
    return web.json_response(result)


async def choose_card(request: web.Request) -> web.Response:
    session_id = request.match_info["session_id"]
    body = await request.json()
    result = bots[session_id].choose_card(body)
    return web.json_response(result)


async def notify(request: web.Request) -> web.Response:
    session_id = request.match_info["session_id"]
    event_name = request.match_info["event"]
    body = await request.json()
    bot = bots[session_id]

    handlers = {
        "deal-started": bot.on_deal_started,
        "card-played": bot.on_card_played,
        "trick-completed": bot.on_trick_completed,
        "deal-ended": bot.on_deal_ended,
        "match-ended": bot.on_match_ended,
    }
    handler = handlers.get(event_name)
    if handler:
        handler(body)

    return web.Response(status=200)


app = web.Application()
app.router.add_get("/health", health)
app.router.add_post("/api/sessions", create_session)
app.router.add_delete("/api/sessions/{session_id}", delete_session)
app.router.add_post("/api/sessions/{session_id}/choose-cut", choose_cut)
app.router.add_post("/api/sessions/{session_id}/choose-negotiation-action", choose_negotiation_action)
app.router.add_post("/api/sessions/{session_id}/choose-card", choose_card)
app.router.add_post("/api/sessions/{session_id}/notify/{event}", notify)

if __name__ == "__main__":
    print(f"random-python-bot listening on port {PORT}")
    web.run_app(app, host="0.0.0.0", port=PORT, print=None)
