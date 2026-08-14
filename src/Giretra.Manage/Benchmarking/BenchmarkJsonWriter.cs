using System.Text.Json;

namespace Giretra.Manage.Benchmarking;

/// <summary>
/// Serializes a benchmark result to a machine-readable JSON file,
/// consumed by scripts such as tools/ab-bench.sh.
/// </summary>
public static class BenchmarkJsonWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Write(string path, BenchmarkResult result, BenchmarkConfig config)
    {
        var ci1 = result.Team1WinRateConfidenceInterval;
        var ci2 = result.Team2WinRateConfidenceInterval;

        var payload = new
        {
            Config = new
            {
                config.MatchCount,
                config.TargetScore,
                config.Seed,
                config.Shuffle,
                config.EloKFactor
            },
            Team1 = new
            {
                Name = result.Team1Name,
                Wins = result.Team1Wins,
                WinRate = result.Team1WinRate,
                Ci95Lower = ci1.Lower,
                Ci95Upper = ci1.Upper,
                InitialElo = result.Team1InitialElo,
                FinalElo = result.Team1FinalElo
            },
            Team2 = new
            {
                Name = result.Team2Name,
                Wins = result.Team2Wins,
                WinRate = result.Team2WinRate,
                Ci95Lower = ci2.Lower,
                Ci95Upper = ci2.Upper,
                InitialElo = result.Team2InitialElo,
                FinalElo = result.Team2FinalElo
            },
            result.TotalMatches,
            result.TotalDeals,
            result.AverageDealsPerMatch,
            DurationSeconds = result.TotalDuration.TotalSeconds,
            result.PValue,
            Significant = result.IsSignificant,
            GameModes = result.GetGameModeStats()
                .Where(s => s.TotalDeals > 0)
                .Select(s => new
                {
                    GameMode = s.GameMode.ToString(),
                    s.TotalDeals,
                    Team1Announced = new
                    {
                        s.Team1Announced.Announced,
                        s.Team1Announced.AnnouncerWins,
                        s.Team1Announced.AnnouncerWinRate
                    },
                    Team2Announced = new
                    {
                        s.Team2Announced.Announced,
                        s.Team2Announced.AnnouncerWins,
                        s.Team2Announced.AnnouncerWinRate
                    }
                })
        };

        File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions));
    }
}
