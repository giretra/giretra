using System.Collections.Immutable;
using System.Diagnostics;
using Giretra.Core;
using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.Players.Factories;

namespace Giretra.Manage.Validation;

/// <summary>
/// Orchestrates validation matches, wrapping the target bot in <see cref="ValidatingPlayerAgent"/>.
/// </summary>
public sealed class ValidationRunner
{
    private readonly IPlayerAgentFactory _agentFactory;
    private readonly IPlayerAgentFactory _opponentFactory;
    private readonly ValidationConfig _config;

    public event Action<ValidationMatchProgress>? OnMatchCompleted;

    public ValidationRunner(
        IPlayerAgentFactory agentFactory,
        IPlayerAgentFactory opponentFactory,
        ValidationConfig config)
    {
        _agentFactory = agentFactory;
        _opponentFactory = opponentFactory;
        _config = config;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _agentFactory.InitializeAsync(cancellationToken);
        await _opponentFactory.InitializeAsync(cancellationToken);
    }

    public async Task<ValidationResult> RunAsync(bool recordDecisions = false)
    {
        var allViolations = ImmutableList.CreateBuilder<Violation>();
        var allTimings = new List<DecisionTiming>();
        var allNotificationWarnings = ImmutableList.CreateBuilder<NotificationWarning>();
        var allCrashDetails = ImmutableList.CreateBuilder<string>();
        var gameModesCovered = ImmutableHashSet.CreateBuilder<GameMode>();

        var totalStopwatch = Stopwatch.StartNew();

        var deckRandom = _config.Shuffle
            ? (_config.Seed.HasValue ? new Random(_config.Seed.Value + 50000) : new Random())
            : null;

        int matchesCompleted = 0;
        int matchesCrashed = 0;
        int totalDeals = 0;
        int agentTeamWins = 0;
        int opponentTeamWins = 0;

        for (int i = 0; i < _config.MatchCount; i++)
        {
            var matchStopwatch = Stopwatch.StartNew();
            int matchViolationsBefore = allViolations.Count;

            // Create fresh agents — target bot is Team1 (Bottom + Top), wrapped
            var bottomInner = _agentFactory.Create(PlayerPosition.Bottom);
            var topInner = _agentFactory.Create(PlayerPosition.Top);
            var bottomAgent = new ValidatingPlayerAgent(bottomInner, _config.TimeoutMs, recordDecisions);
            var topAgent = new ValidatingPlayerAgent(topInner, _config.TimeoutMs, recordDecisions);

            // Opponent is Team2 (Left + Right), unwrapped
            var left = _opponentFactory.Create(PlayerPosition.Left);
            var right = _opponentFactory.Create(PlayerPosition.Right);

            bottomAgent.SetMatchNumber(i + 1);
            topAgent.SetMatchNumber(i + 1);

            var firstDealer = (PlayerPosition)(i % 4);

            Func<Deck> deckProvider = deckRandom is not null
                ? () => Deck.CreateShuffled(deckRandom)
                : Deck.CreateShuffled;

            int dealsPlayed = 0;
            bool crashed = false;

            try
            {
                var gameManager = new GameManager(
                    bottomAgent, left, topAgent, right,
                    firstDealer, deckProvider, _config.TargetScore);
                var matchState = await gameManager.PlayMatchAsync();

                dealsPlayed = matchState.CompletedDeals.Count;
                var winner = matchState.Winner!.Value;

                if (winner == Team.Team1)
                    agentTeamWins++;
                else
                    opponentTeamWins++;

                // Collect game modes from completed deals
                foreach (var deal in matchState.CompletedDeals)
                    gameModesCovered.Add(deal.GameMode);

                matchesCompleted++;
            }
            catch (Exception ex)
            {
                crashed = true;
                matchesCrashed++;
                allCrashDetails.Add($"Match {i + 1}: {ex.GetType().Name}: {ex.Message}");
            }

            totalDeals += dealsPlayed;

            // Collect violations and timings from both wrapped agents
            allViolations.AddRange(bottomAgent.Violations);
            allViolations.AddRange(topAgent.Violations);
            allTimings.AddRange(bottomAgent.Timings);
            allTimings.AddRange(topAgent.Timings);
            allNotificationWarnings.AddRange(bottomAgent.NotificationWarnings);
            allNotificationWarnings.AddRange(topAgent.NotificationWarnings);

            matchStopwatch.Stop();

            int matchViolations = allViolations.Count - matchViolationsBefore;

            OnMatchCompleted?.Invoke(new ValidationMatchProgress
            {
                MatchNumber = i + 1,
                ViolationCount = matchViolations,
                CumulativeViolations = allViolations.Count,
                Crashed = crashed,
                DealsPlayed = dealsPlayed,
                Duration = matchStopwatch.Elapsed
            });
        }

        totalStopwatch.Stop();

        // Compute timing stats per decision type
        var timingStats = ComputeTimingStats(allTimings);

        // Compute performance trend
        var performanceTrend = ComputePerformanceTrend(allTimings, _config.MatchCount);

        return new ValidationResult
        {
            MatchesCompleted = matchesCompleted,
            MatchesCrashed = matchesCrashed,
            TotalDeals = totalDeals,
            Violations = allViolations.ToImmutable(),
            TimingStats = timingStats,
            GameModesCovered = gameModesCovered.ToImmutable(),
            AgentTeamWins = agentTeamWins,
            OpponentTeamWins = opponentTeamWins,
            CrashDetails = allCrashDetails.ToImmutable(),
            TotalDuration = totalStopwatch.Elapsed,
            NotificationWarnings = allNotificationWarnings.ToImmutable(),
            DeterminismViolations = ImmutableList<DeterminismViolation>.Empty,
            PerformanceTrend = performanceTrend
        };
    }

    /// <summary>
    /// Runs the determinism check: plays the same matches twice with the same seed and compares decisions.
    /// Uses a seeded <see cref="RandomPlayerAgentFactory"/> as opponent to ensure identical game states.
    /// </summary>
    public async Task<ImmutableList<DeterminismViolation>> RunDeterminismCheckAsync()
    {
        var seed = _config.Seed ?? Random.Shared.Next();

        var config1 = new ValidationConfig
        {
            MatchCount = _config.MatchCount,
            TargetScore = _config.TargetScore,
            Seed = seed,
            Shuffle = _config.Shuffle,
            TimeoutMs = _config.TimeoutMs,
            Determinism = false,
            Verbose = _config.Verbose
        };

        // Use seeded opponent factories so both runs see identical game states
        var opponentSeed = seed + 99999;
        var opponent1 = new RandomPlayerAgentFactory(opponentSeed);
        var opponent2 = new RandomPlayerAgentFactory(opponentSeed);

        var runner1 = new ValidationRunner(_agentFactory, opponent1, config1);
        var result1 = await runner1.RunWithDecisionRecordingAsync();

        var runner2 = new ValidationRunner(_agentFactory, opponent2, config1);
        var result2 = await runner2.RunWithDecisionRecordingAsync();

        return CompareDeterminism(result1, result2);
    }

    internal async Task<IReadOnlyList<(DecisionType Type, string Value)>> RunWithDecisionRecordingAsync()
    {
        var allDecisions = new List<(DecisionType Type, string Value)>();

        var deckRandom = _config.Shuffle
            ? (_config.Seed.HasValue ? new Random(_config.Seed.Value + 50000) : new Random())
            : null;

        for (int i = 0; i < _config.MatchCount; i++)
        {
            var bottomInner = _agentFactory.Create(PlayerPosition.Bottom);
            var topInner = _agentFactory.Create(PlayerPosition.Top);
            var bottomAgent = new ValidatingPlayerAgent(bottomInner, _config.TimeoutMs, recordDecisions: true);
            var topAgent = new ValidatingPlayerAgent(topInner, _config.TimeoutMs, recordDecisions: true);

            var left = _opponentFactory.Create(PlayerPosition.Left);
            var right = _opponentFactory.Create(PlayerPosition.Right);

            bottomAgent.SetMatchNumber(i + 1);
            topAgent.SetMatchNumber(i + 1);

            var firstDealer = (PlayerPosition)(i % 4);

            Func<Deck> deckProvider = deckRandom is not null
                ? () => Deck.CreateShuffled(deckRandom)
                : Deck.CreateShuffled;

            try
            {
                var gameManager = new GameManager(
                    bottomAgent, left, topAgent, right,
                    firstDealer, deckProvider, _config.TargetScore);
                await gameManager.PlayMatchAsync();
            }
            catch
            {
                // Match crashed — decisions up to this point are still recorded
            }

            allDecisions.AddRange(bottomAgent.Decisions);
            allDecisions.AddRange(topAgent.Decisions);
        }

        return allDecisions;
    }

    private static ImmutableList<DeterminismViolation> CompareDeterminism(
        IReadOnlyList<(DecisionType Type, string Value)> run1,
        IReadOnlyList<(DecisionType Type, string Value)> run2)
    {
        var violations = ImmutableList.CreateBuilder<DeterminismViolation>();
        var count = Math.Min(run1.Count, run2.Count);

        for (int i = 0; i < count; i++)
        {
            if (run1[i].Type != run2[i].Type || run1[i].Value != run2[i].Value)
            {
                violations.Add(new DeterminismViolation(
                    run1[i].Type, i, run1[i].Value, run2[i].Value));
            }
        }

        // If runs have different lengths, that's also a violation
        if (run1.Count != run2.Count)
        {
            violations.Add(new DeterminismViolation(
                DecisionType.CardPlay,
                count,
                $"Run 1 had {run1.Count} decisions",
                $"Run 2 had {run2.Count} decisions"));
        }

        return violations.ToImmutable();
    }

    private static ImmutableList<DecisionTimingStats> ComputeTimingStats(List<DecisionTiming> timings)
    {
        var stats = ImmutableList.CreateBuilder<DecisionTimingStats>();

        foreach (var type in Enum.GetValues<DecisionType>())
        {
            var durations = timings
                .Where(t => t.Type == type)
                .Select(t => t.Duration)
                .ToList();

            stats.Add(DecisionTimingStats.Compute(type, durations));
        }

        return stats.ToImmutable();
    }

    private static ImmutableList<PerformanceTrendEntry> ComputePerformanceTrend(
        List<DecisionTiming> timings, int matchCount)
    {
        var trend = ImmutableList.CreateBuilder<PerformanceTrendEntry>();
        if (matchCount < 2) return trend.ToImmutable();

        var halfPoint = matchCount / 2;

        foreach (var type in Enum.GetValues<DecisionType>())
        {
            var firstHalf = timings
                .Where(t => t.Type == type && t.MatchNumber <= halfPoint)
                .Select(t => t.Duration)
                .OrderBy(d => d)
                .ToList();

            var secondHalf = timings
                .Where(t => t.Type == type && t.MatchNumber > halfPoint)
                .Select(t => t.Duration)
                .OrderBy(d => d)
                .ToList();

            if (firstHalf.Count == 0 || secondHalf.Count == 0)
                continue;

            var firstMedian = firstHalf[firstHalf.Count / 2];
            var secondMedian = secondHalf[secondHalf.Count / 2];

            var hasDegradation = firstMedian.Ticks > 0 &&
                                 secondMedian.Ticks > firstMedian.Ticks * 2;

            trend.Add(new PerformanceTrendEntry(type, firstMedian, secondMedian, hasDegradation));
        }

        return trend.ToImmutable();
    }
}
