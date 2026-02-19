using System.Collections.Immutable;
using System.Diagnostics;
using Giretra.Manage.Elo;
using Giretra.Core;
using Giretra.Core.Cards;
using Giretra.Core.Players;

namespace Giretra.Manage.Swiss;

/// <summary>
/// Orchestrates a Swiss tournament between multiple agent factories.
/// </summary>
public sealed class SwissRunner
{
    private readonly List<SwissParticipant> _participants;
    private readonly SwissConfig _config;

    public event Action<SwissRoundResult>? OnRoundCompleted;
    public event Action<int, SwissMatchResult>? OnMatchCompleted;

    public SwissRunner(IReadOnlyList<IPlayerAgentFactory> factories, SwissConfig config)
    {
        _config = config;
        _participants = factories
            .Select(f => new SwissParticipant(f, config.InitialElo))
            .ToList();
    }

    public async Task<SwissTournamentResult> RunAsync()
    {
        var totalStopwatch = Stopwatch.StartNew();
        var rounds = ImmutableList.CreateBuilder<SwissRoundResult>();

        var deckRandom = _config.Shuffle
            ? (_config.Seed.HasValue ? new Random(_config.Seed.Value + 50000) : new Random())
            : null;

        for (int round = 1; round <= _config.Rounds; round++)
        {
            var (pairings, bye) = SwissPairing.PairRound(_participants);
            var matchResults = ImmutableList.CreateBuilder<SwissMatchResult>();

            if (bye is not null)
            {
                bye.Byes++;
            }

            foreach (var (p1, p2) in pairings)
            {
                var matchResult = await PlayMatchAsync(p1, p2, round, deckRandom);
                matchResults.Add(matchResult);
                OnMatchCompleted?.Invoke(round, matchResult);
            }

            var roundResult = new SwissRoundResult
            {
                RoundNumber = round,
                Matches = matchResults.ToImmutable(),
                ByeRecipient = bye
            };

            rounds.Add(roundResult);
            OnRoundCompleted?.Invoke(roundResult);
        }

        totalStopwatch.Stop();

        var ranked = _participants
            .OrderByDescending(p => p.Points)
            .ThenByDescending(p => p.Elo)
            .ToImmutableList();

        return new SwissTournamentResult
        {
            RankedParticipants = ranked,
            Rounds = rounds.ToImmutable(),
            TotalDuration = totalStopwatch.Elapsed
        };
    }

    private async Task<SwissMatchResult> PlayMatchAsync(
        SwissParticipant p1, SwissParticipant p2, int round, Random? deckRandom)
    {
        var matchStopwatch = Stopwatch.StartNew();

        var bottom = p1.Factory.Create(PlayerPosition.Bottom);
        var top = p1.Factory.Create(PlayerPosition.Top);
        var left = p2.Factory.Create(PlayerPosition.Left);
        var right = p2.Factory.Create(PlayerPosition.Right);

        var firstDealer = (PlayerPosition)(round % 4);

        Func<Deck> deckProvider = deckRandom is not null
            ? () => Deck.CreateShuffled(deckRandom)
            : Deck.CreateShuffled;

        var gameManager = new GameManager(bottom, left, top, right, firstDealer, deckProvider, _config.TargetScore);
        var matchState = await gameManager.PlayMatchAsync();

        matchStopwatch.Stop();

        var team1Won = matchState.Winner!.Value == Team.Team1;

        // Compute per-participant decaying K-factor
        var k1 = EloCalculator.EffectiveKFactor(
            _config.EloKFactorMax, _config.EloKFactorMin, p1.MatchesPlayed, _config.EloKFactorHalfLife);
        var k2 = EloCalculator.EffectiveKFactor(
            _config.EloKFactorMax, _config.EloKFactorMin, p2.MatchesPlayed, _config.EloKFactorHalfLife);

        // Compute margin-based actual scores
        var winnerScore = team1Won ? matchState.Team1MatchPoints : matchState.Team2MatchPoints;
        var loserScore = team1Won ? matchState.Team2MatchPoints : matchState.Team1MatchPoints;
        var marginActual = EloCalculator.MarginScore(winnerScore, loserScore, _config.TargetScore);
        var actual1 = team1Won ? marginActual : 1.0 - marginActual;
        var actual2 = team1Won ? 1.0 - marginActual : marginActual;

        var (newP1Elo, newP2Elo) = EloCalculator.CalculateNewRatings(
            p1.Elo, p2.Elo, actual1, actual2, k1, k2);

        var eloChange = Math.Abs(newP1Elo - p1.Elo);

        p1.UpdateElo(newP1Elo);
        p2.UpdateElo(newP2Elo);

        var winner = team1Won ? p1 : p2;
        var loser = team1Won ? p2 : p1;
        winner.Wins++;
        loser.Losses++;

        return new SwissMatchResult
        {
            Participant1 = p1,
            Participant2 = p2,
            Winner = winner,
            Team1Score = matchState.Team1MatchPoints,
            Team2Score = matchState.Team2MatchPoints,
            DealsPlayed = matchState.CompletedDeals.Count,
            EloChange = eloChange,
            Duration = matchStopwatch.Elapsed
        };
    }
}
