using System.Collections.Concurrent;
using System.Diagnostics;
using Giretra.Core;
using Giretra.Core.Cards;
using Giretra.Core.Players;
using Giretra.Core.Scoring;
using Giretra.Core.Solving;
using Giretra.Core.State;

namespace Giretra.Manage.Analysis;

/// <summary>
/// Plays matches between two agent factories, then reviews every deal with the double-dummy solver.
/// </summary>
public sealed class AnalysisRunner
{
    // Same Colour sweep rule as the benchmark, so both commands score deals alike
    private const int ColourSweepMatchPoints = 25;

    private readonly IPlayerAgentFactory _team1Factory;
    private readonly IPlayerAgentFactory _team2Factory;
    private readonly int _matchCount;
    private readonly int _targetScore;
    private readonly int? _seed;

    /// <summary>
    /// Fired after each match is played, with the number of matches played so far.
    /// </summary>
    public event Action<int>? OnMatchPlayed;

    /// <summary>
    /// Fired after each deal is analysed, with the number of deals analysed so far and the total.
    /// </summary>
    public event Action<int, int>? OnDealAnalyzed;

    public AnalysisRunner(
        IPlayerAgentFactory team1Factory,
        IPlayerAgentFactory team2Factory,
        int matchCount,
        int targetScore,
        int? seed)
    {
        _team1Factory = team1Factory;
        _team2Factory = team2Factory;
        _matchCount = matchCount;
        _targetScore = targetScore;
        _seed = seed;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _team1Factory.InitializeAsync(cancellationToken);
        await _team2Factory.InitializeAsync(cancellationToken);
    }

    public async Task<AnalysisResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (_seed.HasValue)
        {
            _team1Factory.Seed = _seed.Value;
            _team2Factory.Seed = _seed.Value + 10000;
        }

        var deckRandom = _seed.HasValue ? new Random(_seed.Value + 50000) : new Random();
        var recorded = new List<(int Match, int Deal, DealResult Result, HandState Hand)>();
        var team1Wins = 0;

        var playStopwatch = Stopwatch.StartNew();
        for (var i = 0; i < _matchCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var matchNumber = i + 1;
            var dealNumber = 0;

            // One recorder is enough: every player is told about every deal
            var bottom = new RecordingPlayerAgent(
                _team1Factory.Create(PlayerPosition.Bottom),
                (result, hand) => recorded.Add((matchNumber, ++dealNumber, result, hand)));
            var top = _team1Factory.Create(PlayerPosition.Top);
            var left = _team2Factory.Create(PlayerPosition.Left);
            var right = _team2Factory.Create(PlayerPosition.Right);

            var gameManager = new GameManager(bottom, left, top, right, (PlayerPosition)(i % 4),
                () => Deck.CreateShuffled(deckRandom), _targetScore, ColourSweepMatchPoints);
            var matchState = await gameManager.PlayMatchAsync(cancellationToken);

            if (matchState.Winner == Team.Team1) team1Wins++;
            OnMatchPlayed?.Invoke(matchNumber);
        }

        playStopwatch.Stop();

        var solverStopwatch = Stopwatch.StartNew();
        var analyzed = new ConcurrentBag<AnalyzedDeal>();
        var done = 0;

        Parallel.ForEach(
            recorded,
            new ParallelOptions { CancellationToken = cancellationToken },
            deal =>
            {
                var scoring = new SolverScoring(deal.Result.AnnouncerTeam, deal.Result.Multiplier, ColourSweepMatchPoints);
                analyzed.Add(new AnalyzedDeal(deal.Match, deal.Deal, deal.Result, HandAnalyzer.Analyze(deal.Hand, scoring)));
                OnDealAnalyzed?.Invoke(Interlocked.Increment(ref done), recorded.Count);
            });

        solverStopwatch.Stop();

        return new AnalysisResult
        {
            Team1Name = _team1Factory.DisplayName,
            Team2Name = _team2Factory.DisplayName,
            Matches = _matchCount,
            Team1Wins = team1Wins,
            Team2Wins = _matchCount - team1Wins,
            Deals = analyzed.OrderBy(d => d.MatchNumber).ThenBy(d => d.DealNumber).ToList(),
            PlayDuration = playStopwatch.Elapsed,
            SolverDuration = solverStopwatch.Elapsed
        };
    }
}
