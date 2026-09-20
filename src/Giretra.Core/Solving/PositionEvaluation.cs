using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Core.Solving;

/// <summary>
/// The solved value of playing one card from a position.
/// </summary>
/// <param name="Card">The card evaluated.</param>
/// <param name="Outcome">The end-of-hand result if this card is played and everyone plays perfectly afterwards.</param>
/// <param name="Score">The outcome's solver score from the playing team's perspective (see <see cref="SolvedOutcome.GetScore"/>).</param>
/// <param name="CardPointLoss">Card points given up compared to the best card (0 for an optimal card).</param>
/// <param name="IsOptimal">Whether no other valid card achieves a better outcome.</param>
public sealed record CardEvaluation(
    Card Card,
    SolvedOutcome Outcome,
    int Score,
    int CardPointLoss,
    bool IsOptimal);

/// <summary>
/// The solved values of every valid card for the player to move.
/// </summary>
public sealed class PositionEvaluation
{
    /// <summary>
    /// Gets the game mode of the solved hand.
    /// </summary>
    public GameMode GameMode { get; }

    /// <summary>
    /// Gets the player to move.
    /// </summary>
    public PlayerPosition Player { get; }

    /// <summary>
    /// Gets one evaluation per valid card, best first.
    /// </summary>
    public IReadOnlyList<CardEvaluation> Cards { get; }

    /// <summary>
    /// Gets the evaluation of the best card (the first of the optimal cards).
    /// </summary>
    public CardEvaluation Best => Cards[0];

    internal PositionEvaluation(GameMode gameMode, PlayerPosition player, IReadOnlyList<CardEvaluation> cards)
    {
        GameMode = gameMode;
        Player = player;
        Cards = cards;
    }

    /// <summary>
    /// Gets the evaluation of a specific valid card.
    /// </summary>
    public CardEvaluation GetCard(Card card)
    {
        foreach (var evaluation in Cards)
        {
            if (evaluation.Card == card) return evaluation;
        }

        throw new ArgumentException($"{card} is not a valid play for {Player} in this position.", nameof(card));
    }

    /// <summary>
    /// Gets the match points a card gives up compared to the best card, from the playing team's perspective.
    /// </summary>
    public int GetMatchPointLoss(Card card, SolverScoring scoring)
    {
        var team = Player.GetTeam();
        return scoring.GetMatchPointSwing(GameMode, Best.Outcome, team)
               - scoring.GetMatchPointSwing(GameMode, GetCard(card).Outcome, team);
    }
}
