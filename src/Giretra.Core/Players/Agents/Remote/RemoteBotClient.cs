using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Play;
using Giretra.Core.Scoring;
using Giretra.Core.State;

namespace Giretra.Core.Players.Agents.Remote;

/// <summary>
/// HTTP client for communicating with a remote bot server.
/// Handles serialization of game state to the wire format defined in the Remote Bot API spec.
/// </summary>
public sealed class RemoteBotClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly TimeSpan _decisionTimeout;
    private readonly TimeSpan _notificationTimeout;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public RemoteBotClient(
        HttpClient httpClient,
        bool ownsHttpClient = false,
        TimeSpan? decisionTimeout = null,
        TimeSpan? notificationTimeout = null)
    {
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
        _decisionTimeout = decisionTimeout ?? TimeSpan.FromSeconds(30);
        _notificationTimeout = notificationTimeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task<string> CreateSessionAsync(PlayerPosition position, string matchId)
    {
        var request = new { position = position.ToString(), matchId };
        var response = await PostAsync("api/sessions", request, _decisionTimeout);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CreateSessionResponse>(JsonOptions);
        return result?.SessionId ?? throw new InvalidOperationException("Remote bot returned null session ID.");
    }

    public async Task DestroySessionAsync(string sessionId)
    {
        using var cts = new CancellationTokenSource(_notificationTimeout);
        try
        {
            var response = await _httpClient.DeleteAsync($"api/sessions/{sessionId}", cts.Token);
            // Idempotent — 204 or 404 are both acceptable
        }
        catch (OperationCanceledException)
        {
            // Best-effort cleanup, don't throw
        }
    }

    public async Task<(int Position, bool FromTop)> ChooseCutAsync(
        string sessionId, int deckSize, MatchState matchState)
    {
        var request = new
        {
            deckSize,
            matchState = MapMatchState(matchState)
        };

        var response = await PostAsync($"api/sessions/{sessionId}/choose-cut", request, _decisionTimeout);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChooseCutResponse>(JsonOptions);
        return result is null
            ? throw new InvalidOperationException("Remote bot returned null cut response.")
            : (result.Position, result.FromTop);
    }

    public async Task<NegotiationAction> ChooseNegotiationActionAsync(
        string sessionId,
        PlayerPosition botPosition,
        IReadOnlyList<Card> hand,
        NegotiationState negotiationState,
        MatchState matchState,
        IReadOnlyList<NegotiationAction> validActions)
    {
        var request = new
        {
            hand = hand.Select(MapCard).ToArray(),
            negotiationState = MapNegotiationState(negotiationState),
            matchState = MapMatchState(matchState),
            validActions = validActions.Select(MapNegotiationActionChoice).ToArray()
        };

        var response = await PostAsync(
            $"api/sessions/{sessionId}/choose-negotiation-action", request, _decisionTimeout);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<NegotiationActionResponse>(JsonOptions);
        if (result is null)
            throw new InvalidOperationException("Remote bot returned null negotiation action.");

        return ResolveNegotiationAction(result, botPosition, validActions);
    }

    public async Task<Card> ChooseCardAsync(
        string sessionId,
        IReadOnlyList<Card> hand,
        HandState handState,
        MatchState matchState,
        IReadOnlyList<Card> validPlays)
    {
        var request = new
        {
            hand = hand.Select(MapCard).ToArray(),
            handState = MapHandState(handState),
            matchState = MapMatchState(matchState),
            validPlays = validPlays.Select(MapCard).ToArray()
        };

        var response = await PostAsync(
            $"api/sessions/{sessionId}/choose-card", request, _decisionTimeout);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CardDto>(JsonOptions);
        if (result is null)
            throw new InvalidOperationException("Remote bot returned null card response.");

        return ResolveCard(result, validPlays);
    }

    public async Task NotifyDealStartedAsync(string sessionId, MatchState matchState)
    {
        var request = new { matchState = MapMatchState(matchState) };
        await PostNotificationAsync($"api/sessions/{sessionId}/notify/deal-started", request);
    }

    public async Task NotifyCardPlayedAsync(
        string sessionId, PlayerPosition player, Card card, HandState handState, MatchState matchState)
    {
        var request = new
        {
            player = player.ToString(),
            card = MapCard(card),
            handState = MapHandState(handState),
            matchState = MapMatchState(matchState)
        };
        await PostNotificationAsync($"api/sessions/{sessionId}/notify/card-played", request);
    }

    public async Task NotifyTrickCompletedAsync(
        string sessionId, TrickState completedTrick, PlayerPosition winner,
        HandState handState, MatchState matchState)
    {
        var request = new
        {
            completedTrick = MapTrickState(completedTrick),
            winner = winner.ToString(),
            handState = MapHandState(handState),
            matchState = MapMatchState(matchState)
        };
        await PostNotificationAsync($"api/sessions/{sessionId}/notify/trick-completed", request);
    }

    public async Task NotifyDealEndedAsync(
        string sessionId, DealResult result, HandState handState, MatchState matchState)
    {
        var request = new
        {
            result = MapDealResult(result),
            handState = MapHandState(handState),
            matchState = MapMatchState(matchState)
        };
        await PostNotificationAsync($"api/sessions/{sessionId}/notify/deal-ended", request);
    }

    public async Task NotifyMatchEndedAsync(string sessionId, MatchState matchState)
    {
        var request = new { matchState = MapMatchState(matchState) };
        await PostNotificationAsync($"api/sessions/{sessionId}/notify/match-ended", request);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    #region HTTP helpers

    /// <summary>
    /// Serializes <paramref name="body"/> to a <see cref="StringContent"/> with an
    /// explicit Content-Length. This avoids chunked transfer encoding, which simple
    /// HTTP servers (e.g. Python's http.server) don't support.
    /// </summary>
    private static StringContent ToJsonContent(object body)
    {
        var json = JsonSerializer.Serialize(body, JsonOptions);
        return new StringContent(json, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") { CharSet = "utf-8" });
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object body, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        using var content = ToJsonContent(body);
        try
        {
            return await _httpClient.PostAsync(path, content, cts.Token);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException(
                $"Request to {_httpClient.BaseAddress}{path} failed: {ex.Message}", ex, ex.StatusCode);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Request to {_httpClient.BaseAddress}{path} timed out after {timeout.TotalSeconds}s.");
        }
    }

    private async Task PostNotificationAsync(string path, object body)
    {
        using var cts = new CancellationTokenSource(_notificationTimeout);
        using var content = ToJsonContent(body);
        try
        {
            var response = await _httpClient.PostAsync(path, content, cts.Token);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException(
                $"Notification to {_httpClient.BaseAddress}{path} failed: {ex.Message}", ex, ex.StatusCode);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Notification to {_httpClient.BaseAddress}{path} timed out after {_notificationTimeout.TotalSeconds}s.");
        }
    }

    #endregion

    #region Mapping: Domain → Wire

    private static CardDto MapCard(Card card)
        => new(card.Rank.ToString(), card.Suit.ToString());

    private static object MapPlayedCard(PlayedCard pc)
        => new { player = pc.Player.ToString(), card = MapCard(pc.Card) };

    private static object MapTrickState(TrickState trick)
        => new
        {
            leader = trick.Leader.ToString(),
            trickNumber = trick.TrickNumber,
            playedCards = trick.PlayedCards.Select(MapPlayedCard).ToArray(),
            isComplete = trick.IsComplete
        };

    private static object MapHandState(HandState state)
        => new
        {
            gameMode = state.GameMode.ToString(),
            team1CardPoints = state.Team1CardPoints,
            team2CardPoints = state.Team2CardPoints,
            team1TricksWon = state.Team1TricksWon,
            team2TricksWon = state.Team2TricksWon,
            currentTrick = state.CurrentTrick is not null ? MapTrickState(state.CurrentTrick) : null,
            completedTricks = state.CompletedTricks.Select(MapTrickState).ToArray()
        };

    private static object MapNegotiationState(NegotiationState state)
        => new
        {
            dealer = state.Dealer.ToString(),
            currentPlayer = state.CurrentPlayer.ToString(),
            currentBid = state.CurrentBid?.ToString(),
            currentBidder = state.CurrentBidder?.ToString(),
            consecutiveAccepts = state.ConsecutiveAccepts,
            hasDoubleOccurred = state.HasDoubleOccurred,
            actions = state.Actions.Select(MapNegotiationActionFull).ToArray(),
            doubledModes = state.DoubledModes.ToDictionary(
                kv => kv.Key.ToString(), kv => kv.Value),
            redoubledModes = state.RedoubledModes.Select(m => m.ToString()).ToArray(),
            teamColourAnnouncements = state.TeamColourAnnouncements.ToDictionary(
                kv => kv.Key.ToString(), kv => kv.Value.ToString())
        };

    private static object MapMatchState(MatchState state)
        => new
        {
            targetScore = state.TargetScore,
            team1MatchPoints = state.Team1MatchPoints,
            team2MatchPoints = state.Team2MatchPoints,
            currentDealer = state.CurrentDealer.ToString(),
            isComplete = state.IsComplete,
            winner = state.Winner?.ToString(),
            completedDeals = state.CompletedDeals.Select(MapDealResult).ToArray()
        };

    private static object MapDealResult(DealResult result)
        => new
        {
            gameMode = result.GameMode.ToString(),
            multiplier = result.Multiplier.ToString(),
            announcerTeam = result.AnnouncerTeam.ToString(),
            team1CardPoints = result.Team1CardPoints,
            team2CardPoints = result.Team2CardPoints,
            team1MatchPoints = result.Team1MatchPoints,
            team2MatchPoints = result.Team2MatchPoints,
            wasSweep = result.WasSweep,
            sweepingTeam = result.SweepingTeam?.ToString(),
            isInstantWin = result.IsInstantWin
        };

    /// <summary>
    /// Maps a NegotiationAction for the history (includes player field).
    /// </summary>
    private static object MapNegotiationActionFull(NegotiationAction action)
        => action switch
        {
            AnnouncementAction a => new
            {
                type = "Announcement",
                player = a.Player.ToString(),
                mode = a.Mode.ToString()
            },
            AcceptAction a => new
            {
                type = "Accept",
                player = a.Player.ToString()
            },
            DoubleAction a => new
            {
                type = "Double",
                player = a.Player.ToString(),
                targetMode = a.TargetMode.ToString()
            },
            RedoubleAction a => new
            {
                type = "Redouble",
                player = a.Player.ToString(),
                targetMode = a.TargetMode.ToString()
            },
            _ => throw new ArgumentException($"Unknown negotiation action type: {action.GetType()}")
        };

    /// <summary>
    /// Maps a NegotiationAction for the validActions list (omits player field).
    /// </summary>
    private static object MapNegotiationActionChoice(NegotiationAction action)
        => action switch
        {
            AnnouncementAction a => new { type = "Announcement", mode = a.Mode.ToString() },
            AcceptAction => new { type = "Accept" },
            DoubleAction a => new { type = "Double", targetMode = a.TargetMode.ToString() },
            RedoubleAction a => new { type = "Redouble", targetMode = a.TargetMode.ToString() },
            _ => throw new ArgumentException($"Unknown negotiation action type: {action.GetType()}")
        };

    #endregion

    #region Mapping: Wire → Domain

    private static NegotiationAction ResolveNegotiationAction(
        NegotiationActionResponse response,
        PlayerPosition botPosition,
        IReadOnlyList<NegotiationAction> validActions)
    {
        // Match the response against the valid actions list
        foreach (var action in validActions)
        {
            if (MatchesResponse(action, response))
                return action;
        }

        throw new InvalidOperationException(
            $"Remote bot returned negotiation action that doesn't match any valid action: " +
            $"type={response.Type}, mode={response.Mode}, targetMode={response.TargetMode}");
    }

    private static bool MatchesResponse(NegotiationAction action, NegotiationActionResponse response)
    {
        return action switch
        {
            AnnouncementAction a =>
                string.Equals(response.Type, "Announcement", StringComparison.OrdinalIgnoreCase)
                && string.Equals(response.Mode, a.Mode.ToString(), StringComparison.OrdinalIgnoreCase),

            AcceptAction =>
                string.Equals(response.Type, "Accept", StringComparison.OrdinalIgnoreCase),

            DoubleAction a =>
                string.Equals(response.Type, "Double", StringComparison.OrdinalIgnoreCase)
                && string.Equals(response.TargetMode, a.TargetMode.ToString(), StringComparison.OrdinalIgnoreCase),

            RedoubleAction a =>
                string.Equals(response.Type, "Redouble", StringComparison.OrdinalIgnoreCase)
                && string.Equals(response.TargetMode, a.TargetMode.ToString(), StringComparison.OrdinalIgnoreCase),

            _ => false
        };
    }

    private static Card ResolveCard(CardDto dto, IReadOnlyList<Card> validPlays)
    {
        if (!Enum.TryParse<CardRank>(dto.Rank, ignoreCase: true, out var rank))
            throw new InvalidOperationException($"Remote bot returned unknown card rank: {dto.Rank}");

        if (!Enum.TryParse<CardSuit>(dto.Suit, ignoreCase: true, out var suit))
            throw new InvalidOperationException($"Remote bot returned unknown card suit: {dto.Suit}");

        var card = new Card(rank, suit);

        if (!validPlays.Contains(card))
            throw new InvalidOperationException(
                $"Remote bot returned card {card} which is not in the valid plays list.");

        return card;
    }

    #endregion

    #region Wire DTOs

    private sealed record CreateSessionResponse(string SessionId);

    private sealed record ChooseCutResponse(int Position, bool FromTop);

    private sealed record NegotiationActionResponse(
        string Type,
        string? Mode = null,
        string? TargetMode = null);

    private sealed record CardDto(string Rank, string Suit);

    #endregion
}
