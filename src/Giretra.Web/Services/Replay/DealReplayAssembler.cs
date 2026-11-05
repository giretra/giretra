using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Core.Players;
using Giretra.Core.State;
using Giretra.Web.Models.Responses;

namespace Giretra.Web.Services.Replay;

/// <summary>
/// Turns the 32 recorded plays of a deal into replay tricks and starting hands by driving a
/// real <see cref="HandState"/>, so trick winners and card points (including the last-trick
/// bonus) always come from the engine rather than a second implementation of the rules.
/// </summary>
public static class DealReplayAssembler
{
    public const int PlaysPerDeal = 32;

    public sealed record Result(
        IReadOnlyList<DealReplayTrickResponse> Tricks,
        IReadOnlyList<DealReplayHandResponse> Hands,
        int Team1CardPoints,
        int Team2CardPoints);

    /// <summary>
    /// Replays <paramref name="plays"/> (in play order) under <paramref name="gameMode"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">The plays are not a complete, consistent deal.</exception>
    public static Result Assemble(GameMode gameMode, IReadOnlyList<PlayedCard> plays)
    {
        if (plays.Count != PlaysPerDeal)
            throw new InvalidDataException($"A deal has {PlaysPerDeal} plays, got {plays.Count}.");
        if (plays.Select(p => p.Card).Distinct().Count() != PlaysPerDeal)
            throw new InvalidDataException("The plays contain duplicate cards.");

        var state = HandState.Create(gameMode, plays[0].Player);
        var tricks = new List<DealReplayTrickResponse>(8);

        foreach (var play in plays)
        {
            var expected = state.CurrentTrick?.CurrentPlayer;
            if (expected != play.Player)
            {
                throw new InvalidDataException(
                    $"Play order is inconsistent: expected {expected} to play, recorded {play.Player}.");
            }

            state = state.PlayCard(play.Card);

            if (state.CurrentTrick is null || state.CurrentTrick.PlayedCards.Count == 0)
            {
                var trick = state.CompletedTricks[^1];
                tricks.Add(new DealReplayTrickResponse
                {
                    TrickNumber = trick.TrickNumber,
                    Leader = trick.Leader,
                    Plays = trick.PlayedCards
                        .Select(pc => new PlayedCardResponse
                        {
                            Player = pc.Player,
                            Card = CardResponse.FromCard(pc.Card),
                        })
                        .ToList(),
                    Winner = state.TrickWinners[^1],
                    Team1CumulativeCardPoints = state.Team1CardPoints,
                    Team2CumulativeCardPoints = state.Team2CardPoints,
                });
            }
        }

        var hands = Enum.GetValues<PlayerPosition>()
            .Select(position => new DealReplayHandResponse
            {
                Position = position,
                Cards = plays
                    .Where(p => p.Player == position)
                    .Select(p => p.Card)
                    .OrderBy(c => c.Suit)
                    .ThenByDescending(c => c.GetStrength(gameMode))
                    .Select(CardResponse.FromCard)
                    .ToList(),
            })
            .ToList();

        if (hands.Any(h => h.Cards.Count != 8))
            throw new InvalidDataException("Each player must have played exactly 8 cards.");

        return new Result(tricks, hands, state.Team1CardPoints, state.Team2CardPoints);
    }
}
