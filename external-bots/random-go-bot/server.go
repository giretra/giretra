// server.go — HTTP boilerplate. Bot creators should not need to edit this file.
// All game logic lives in bot.go.

package main

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"strconv"
	"sync"
)

func main() {
	port := os.Getenv("PORT")
	if port == "" {
		port = "5063"
	}

	var bots sync.Map
	mux := http.NewServeMux()

	// ── Health ─────────────────────────────────────────────────────

	mux.HandleFunc("GET /health", func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
	})

	// ── Sessions ───────────────────────────────────────────────────

	mux.HandleFunc("POST /api/sessions", func(w http.ResponseWriter, r *http.Request) {
		var body SessionRequest
		if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
			http.Error(w, err.Error(), http.StatusBadRequest)
			return
		}

		sessionID := newSessionID()
		bots.Store(sessionID, NewBot(body.MatchID))

		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusCreated)
		json.NewEncoder(w).Encode(map[string]string{"sessionId": sessionID})
	})

	mux.HandleFunc("DELETE /api/sessions/{sessionId}", func(w http.ResponseWriter, r *http.Request) {
		bots.Delete(r.PathValue("sessionId"))
		w.WriteHeader(http.StatusNoContent)
	})

	// ── Decisions ──────────────────────────────────────────────────

	mux.HandleFunc("POST /api/sessions/{sessionId}/choose-cut", func(w http.ResponseWriter, r *http.Request) {
		bot, ok := getBot(&bots, r)
		if !ok {
			http.NotFound(w, r)
			return
		}
		var ctx ChooseCutContext
		if err := json.NewDecoder(r.Body).Decode(&ctx); err != nil {
			http.Error(w, err.Error(), http.StatusBadRequest)
			return
		}
		writeJSON(w, bot.ChooseCut(ctx))
	})

	mux.HandleFunc("POST /api/sessions/{sessionId}/choose-negotiation-action", func(w http.ResponseWriter, r *http.Request) {
		bot, ok := getBot(&bots, r)
		if !ok {
			http.NotFound(w, r)
			return
		}
		var ctx ChooseNegotiationActionContext
		if err := json.NewDecoder(r.Body).Decode(&ctx); err != nil {
			http.Error(w, err.Error(), http.StatusBadRequest)
			return
		}
		writeJSON(w, bot.ChooseNegotiationAction(ctx))
	})

	mux.HandleFunc("POST /api/sessions/{sessionId}/choose-card", func(w http.ResponseWriter, r *http.Request) {
		bot, ok := getBot(&bots, r)
		if !ok {
			http.NotFound(w, r)
			return
		}
		var ctx ChooseCardContext
		if err := json.NewDecoder(r.Body).Decode(&ctx); err != nil {
			http.Error(w, err.Error(), http.StatusBadRequest)
			return
		}
		writeJSON(w, bot.ChooseCard(ctx))
	})

	// ── Notifications ──────────────────────────────────────────────

	mux.HandleFunc("POST /api/sessions/{sessionId}/notify/{eventName}", func(w http.ResponseWriter, r *http.Request) {
		bot, ok := getBot(&bots, r)
		if !ok {
			http.NotFound(w, r)
			return
		}

		switch r.PathValue("eventName") {
		case "deal-started":
			var ctx DealStartedContext
			json.NewDecoder(r.Body).Decode(&ctx)
			bot.OnDealStarted(ctx)
		case "card-played":
			var ctx CardPlayedContext
			json.NewDecoder(r.Body).Decode(&ctx)
			bot.OnCardPlayed(ctx)
		case "trick-completed":
			var ctx TrickCompletedContext
			json.NewDecoder(r.Body).Decode(&ctx)
			bot.OnTrickCompleted(ctx)
		case "deal-ended":
			var ctx DealEndedContext
			json.NewDecoder(r.Body).Decode(&ctx)
			bot.OnDealEnded(ctx)
		case "match-ended":
			var ctx MatchEndedContext
			json.NewDecoder(r.Body).Decode(&ctx)
			bot.OnMatchEnded(ctx)
		}

		w.WriteHeader(http.StatusOK)
	})

	// ── Launcher watchdog ─────────────────────────────────────────
	// If LAUNCHER_PID is set, monitor the launcher process and exit if it dies.
	// This prevents orphan bot processes when the launcher crashes.

	if pidStr := os.Getenv("LAUNCHER_PID"); pidStr != "" {
		if pid, err := strconv.Atoi(pidStr); err == nil {
			startLauncherWatchdog(pid)
		}
	}

	fmt.Printf("random-go-bot listening on port %s\n", port)
	http.ListenAndServe("localhost:"+port, mux)
}

func newSessionID() string {
	b := make([]byte, 16)
	rand.Read(b)
	return hex.EncodeToString(b)
}

func getBot(bots *sync.Map, r *http.Request) (*Bot, bool) {
	val, ok := bots.Load(r.PathValue("sessionId"))
	if !ok {
		return nil, false
	}
	return val.(*Bot), true
}

func writeJSON(w http.ResponseWriter, v any) {
	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(v)
}
