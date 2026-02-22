using System.Collections.Immutable;

namespace Giretra.Manage.Swiss;

/// <summary>
/// Result of a single match within a Swiss round.
/// </summary>
public sealed class SwissMatchResult
{
    public required SwissParticipant Participant1 { get; init; }
    public required SwissParticipant Participant2 { get; init; }
    public required SwissParticipant Winner { get; init; }
    public required int Team1Score { get; init; }
    public required int Team2Score { get; init; }
    public required int DealsPlayed { get; init; }
    public required double EloChange { get; init; }
    public required TimeSpan Duration { get; init; }
}

/// <summary>
/// Result of a complete Swiss round.
/// </summary>
public sealed class SwissRoundResult
{
    public required int RoundNumber { get; init; }
    public required ImmutableList<SwissMatchResult> Matches { get; init; }
    public SwissParticipant? ByeRecipient { get; init; }
}
