using Giretra.Core.Players;

namespace Giretra.Web.Domain;

public sealed record MatchPlayerInfo(
    PlayerPosition Position,
    bool IsBot,
    Guid? UserId,
    string? AiAgentType
);
