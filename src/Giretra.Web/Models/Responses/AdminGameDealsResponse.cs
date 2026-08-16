using Giretra.Model.Enums;

namespace Giretra.Web.Models.Responses;

public sealed class AdminGameDealsResponse
{
    public required IReadOnlyList<AdminDealEntry> Deals { get; init; }
}

public sealed class AdminDealEntry
{
    public required short DealNumber { get; init; }
    public required PlayerPosition DealerPosition { get; init; }
    public GameMode? GameMode { get; init; }
    public Team? AnnouncerTeam { get; init; }
    public required MultiplierState Multiplier { get; init; }
    public int? Team1CardPoints { get; init; }
    public int? Team2CardPoints { get; init; }
    public int? Team1MatchPoints { get; init; }
    public int? Team2MatchPoints { get; init; }
    public required bool WasSweep { get; init; }
    public Team? SweepingTeam { get; init; }
    public required bool IsInstantWin { get; init; }
    public bool? AnnouncerWon { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}
