namespace Giretra.Web.Models.Responses;

public sealed class LeaderboardResponse
{
    public required IReadOnlyList<LeaderboardPlayerEntry> Players { get; init; }
    public required IReadOnlyList<LeaderboardBotEntry> Bots { get; init; }
    public required int PlayerCount { get; init; }
    public required int BotCount { get; init; }
}
