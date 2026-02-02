using System.Collections.Immutable;
using Giretra.Core.Cards;
using Giretra.Core.GameModes;
using Giretra.Core.Play;
using Giretra.Core.Players;

namespace Giretra.Core.State;

/// <summary>
/// Represents the state of a hand (8 tricks).
/// </summary>
public sealed class HandState
{
    /// <summary>
    /// Gets the game mode for this hand.
    /// </summary>
    public GameMode GameMode { get; }

    /// <summary>
    /// Gets the completed tricks.
    /// </summary>
    public ImmutableList<TrickState> CompletedTricks { get; }

    /// <summary>
    /// Gets the current trick being played, or null if hand is complete.
    /// </summary>
    public TrickState? CurrentTrick { get; }

    /// <summary>
    /// Gets the card points earned by Team1.
    /// </summary>
    public int Team1CardPoints { get; }

    /// <summary>
    /// Gets the card points earned by Team2.
    /// </summary>
    public int Team2CardPoints { get; }

    /// <summary>
    /// Gets whether all 8 tricks have been played.
    /// </summary>
    public bool IsComplete => CompletedTricks.Count == 8;

    /// <summary>
    /// Gets the number of tricks won by Team1.
    /// </summary>
    public int Team1TricksWon { get; }

    /// <summary>
    /// Gets the number of tricks won by Team2.
    /// </summary>
    public int Team2TricksWon { get; }

    /// <summary>
    /// Checks if a team achieved a sweep (won all 8 tricks).
    /// </summary>
    public Team? SweepingTeam
    {
        get
        {
            if (!IsComplete) return null;
            if (Team1TricksWon == 8) return Team.Team1;
            if (Team2TricksWon == 8) return Team.Team2;
            return null;
        }
    }

    private HandState(
        GameMode gameMode,
        ImmutableList<TrickState> completedTricks,
        TrickState? currentTrick,
        int team1CardPoints,
        int team2CardPoints,
        int team1TricksWon,
        int team2TricksWon)
    {
        GameMode = gameMode;
        CompletedTricks = completedTricks;
        CurrentTrick = currentTrick;
        Team1CardPoints = team1CardPoints;
        Team2CardPoints = team2CardPoints;
        Team1TricksWon = team1TricksWon;
        Team2TricksWon = team2TricksWon;
    }

    /// <summary>
    /// Creates a new hand state with the first player to lead.
    /// </summary>
    public static HandState Create(GameMode gameMode, PlayerPosition firstLeader)
    {
        var firstTrick = TrickState.Create(firstLeader, 1);
        return new HandState(
            gameMode,
            ImmutableList<TrickState>.Empty,
            firstTrick,
            0,
            0,
            0,
            0);
    }

    /// <summary>
    /// Plays a card and returns the new state.
    /// </summary>
    public HandState PlayCard(Card card)
    {
        if (CurrentTrick is null)
        {
            throw new InvalidOperationException("Hand is already complete.");
        }

        var newTrick = CurrentTrick.PlayCard(card);

        if (newTrick.IsComplete)
        {
            return CompleteTrick(newTrick);
        }

        return new HandState(
            GameMode,
            CompletedTricks,
            newTrick,
            Team1CardPoints,
            Team2CardPoints,
            Team1TricksWon,
            Team2TricksWon);
    }

    private HandState CompleteTrick(TrickState completedTrick)
    {
        var winner = DetermineWinner(completedTrick);
        var trickPoints = CalculateTrickPoints(completedTrick);

        var isLastTrick = CompletedTricks.Count == 7;
        if (isLastTrick)
        {
            trickPoints += 10; // Last trick bonus
        }

        var newCompletedTricks = CompletedTricks.Add(completedTrick);
        var winnerTeam = winner.GetTeam();

        var newTeam1Points = Team1CardPoints + (winnerTeam == Team.Team1 ? trickPoints : 0);
        var newTeam2Points = Team2CardPoints + (winnerTeam == Team.Team2 ? trickPoints : 0);
        var newTeam1Tricks = Team1TricksWon + (winnerTeam == Team.Team1 ? 1 : 0);
        var newTeam2Tricks = Team2TricksWon + (winnerTeam == Team.Team2 ? 1 : 0);

        TrickState? nextTrick = null;
        if (newCompletedTricks.Count < 8)
        {
            nextTrick = TrickState.Create(winner, newCompletedTricks.Count + 1);
        }

        return new HandState(
            GameMode,
            newCompletedTricks,
            nextTrick,
            newTeam1Points,
            newTeam2Points,
            newTeam1Tricks,
            newTeam2Tricks);
    }

    private PlayerPosition DetermineWinner(TrickState trick)
    {
        var trumpSuit = GameMode.GetTrumpSuit();
        var leadSuit = trick.LeadSuit!.Value;

        PlayedCard winningCard = trick.PlayedCards[0];

        foreach (var playedCard in trick.PlayedCards.Skip(1))
        {
            if (IsBetter(playedCard, winningCard, leadSuit, trumpSuit))
            {
                winningCard = playedCard;
            }
        }

        return winningCard.Player;
    }

    private bool IsBetter(PlayedCard challenger, PlayedCard current, CardSuit leadSuit, CardSuit? trumpSuit)
    {
        var challengerSuit = challenger.Card.Suit;
        var currentSuit = current.Card.Suit;

        // Trump beats non-trump
        if (trumpSuit.HasValue)
        {
            if (challengerSuit == trumpSuit && currentSuit != trumpSuit)
                return true;
            if (currentSuit == trumpSuit && challengerSuit != trumpSuit)
                return false;
        }

        // If different suits (and neither is trump, or no trump mode), lead suit wins
        if (challengerSuit != currentSuit)
        {
            // In same-suit comparison or when one follows lead
            if (currentSuit == leadSuit && challengerSuit != leadSuit)
                return false;
            if (challengerSuit == leadSuit && currentSuit != leadSuit)
                return true;
            // Neither is lead suit, current holder keeps it
            return false;
        }

        // Same suit: compare strength
        return challenger.Card.GetStrength(GameMode) > current.Card.GetStrength(GameMode);
    }

    private int CalculateTrickPoints(TrickState trick)
    {
        return trick.PlayedCards.Sum(pc => pc.Card.GetPointValue(GameMode));
    }

    /// <summary>
    /// Gets the card points for a specific team.
    /// </summary>
    public int GetCardPoints(Team team)
        => team == Team.Team1 ? Team1CardPoints : Team2CardPoints;

    /// <summary>
    /// Gets the tricks won by a specific team.
    /// </summary>
    public int GetTricksWon(Team team)
        => team == Team.Team1 ? Team1TricksWon : Team2TricksWon;
}
