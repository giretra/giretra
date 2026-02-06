using System.Collections.Immutable;
using System.Diagnostics;
using Giretra.Benchmark.Elo;
using Giretra.Core;
using Giretra.Core.Cards;
using Giretra.Core.Players;

namespace Giretra.Benchmark.Benchmarking;

/// <summary>
/// Orchestrates benchmark matches between two agent factories.
/// </summary>
public sealed class BenchmarkRunner
{
    private readonly IPlayerAgentFactory _team1Factory;
    private readonly IPlayerAgentFactory _team2Factory;
    private readonly BenchmarkConfig _config;

    /// <summary>
    /// Event fired when a match is completed.
    /// </summary>
    public event Action<MatchResult>? OnMatchCompleted;

    public BenchmarkRunner(
        IPlayerAgentFactory team1Factory,
        IPlayerAgentFactory team2Factory,
        BenchmarkConfig config)
    {
        _team1Factory = team1Factory;
        _team2Factory = team2Factory;
        _config = config;
    }

    /// <summary>
    /// Runs the benchmark and returns aggregated results.
    /// </summary>
    public async Task<BenchmarkResult> RunAsync()
    {
        var results = ImmutableList.CreateBuilder<MatchResult>();
        var totalStopwatch = Stopwatch.StartNew();

        // Create random for deck shuffling if enabled
        var deckRandom = _config.Shuffle
            ? (_config.Seed.HasValue ? new Random(_config.Seed.Value + 50000) : new Random())
            : null;

        var team1Elo = _config.Team1InitialElo;
        var team2Elo = _config.Team2InitialElo;
        var team1MinElo = team1Elo;
        var team1MaxElo = team1Elo;
        var team2MinElo = team2Elo;
        var team2MaxElo = team2Elo;
        var team1Wins = 0;
        var team2Wins = 0;
        var totalDeals = 0;

        for (int i = 0; i < _config.MatchCount; i++)
        {
            var matchStopwatch = Stopwatch.StartNew();

            // Create fresh agents for each match
            var bottom = _team1Factory.Create(PlayerPosition.Bottom);
            var top = _team1Factory.Create(PlayerPosition.Top);
            var left = _team2Factory.Create(PlayerPosition.Left);
            var right = _team2Factory.Create(PlayerPosition.Right);

            // Alternate first dealer between matches
            var firstDealer = (PlayerPosition)(i % 4);

            // Create deck provider (shuffled or standard)
            Func<Deck> deckProvider = deckRandom is not null
                ? () => CreateShuffledDeck(deckRandom)
                : Deck.CreateStandard;

            var gameManager = new GameManager(bottom, left, top, right, firstDealer, deckProvider, _config.TargetScore);
            var matchState = await gameManager.PlayMatchAsync();

            matchStopwatch.Stop();

            var winner = matchState.Winner!.Value;
            var team1Won = winner == Team.Team1;

            // Update ELO
            var (newTeam1Elo, newTeam2Elo) = EloCalculator.CalculateNewRatings(
                team1Elo, team2Elo, team1Won, _config.EloKFactor);

            var eloChange = newTeam1Elo - team1Elo;

            var matchResult = new MatchResult
            {
                MatchNumber = i + 1,
                Winner = winner,
                Team1FinalScore = matchState.Team1MatchPoints,
                Team2FinalScore = matchState.Team2MatchPoints,
                DealsPlayed = matchState.CompletedDeals.Count,
                Team1EloAfter = newTeam1Elo,
                Team2EloAfter = newTeam2Elo,
                Team1EloChange = eloChange,
                Duration = matchStopwatch.Elapsed
            };

            results.Add(matchResult);

            team1Elo = newTeam1Elo;
            team2Elo = newTeam2Elo;

            // Track ELO extremes
            team1MinElo = Math.Min(team1MinElo, team1Elo);
            team1MaxElo = Math.Max(team1MaxElo, team1Elo);
            team2MinElo = Math.Min(team2MinElo, team2Elo);
            team2MaxElo = Math.Max(team2MaxElo, team2Elo);

            if (team1Won)
                team1Wins++;
            else
                team2Wins++;

            totalDeals += matchState.CompletedDeals.Count;

            OnMatchCompleted?.Invoke(matchResult);
        }

        totalStopwatch.Stop();

        return new BenchmarkResult
        {
            Team1Name = _team1Factory.AgentName,
            Team2Name = _team2Factory.AgentName,
            Team1InitialElo = _config.Team1InitialElo,
            Team2InitialElo = _config.Team2InitialElo,
            Team1FinalElo = team1Elo,
            Team2FinalElo = team2Elo,
            Team1MinElo = team1MinElo,
            Team1MaxElo = team1MaxElo,
            Team2MinElo = team2MinElo,
            Team2MaxElo = team2MaxElo,
            Team1Wins = team1Wins,
            Team2Wins = team2Wins,
            TotalDeals = totalDeals,
            TotalDuration = totalStopwatch.Elapsed,
            Matches = results.ToImmutable()
        };
    }

    /// <summary>
    /// Creates a shuffled deck using Fisher-Yates shuffle.
    /// </summary>
    private static Deck CreateShuffledDeck(Random random)
    {
        var cards = Deck.CreateStandard().Cards.ToArray();

        // Fisher-Yates shuffle
        for (int i = cards.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        return Deck.FromCards(cards);
    }
}
