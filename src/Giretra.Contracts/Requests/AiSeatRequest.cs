using Giretra.Core.Players;

namespace Giretra.Web.Models.Requests;

/// <summary>
/// Request to assign an AI player of a specific type to a seat.
/// </summary>
public sealed class AiSeatRequest
{
    /// <summary>
    /// The seat position.
    /// </summary>
    public required PlayerPosition Position { get; init; }

    /// <summary>
    /// The AI agent type name (e.g. "CalculatingPlayer", "RandomPlayer").
    /// </summary>
    public required string AiType { get; init; }
}
