using System.Collections.Immutable;
using Giretra.Core.GameModes;
using Giretra.Core.Negotiation;
using Giretra.Core.Players;

namespace Giretra.Core.State;

/// <summary>
/// Tracks the state of negotiation phase with all complex rules.
/// </summary>
public sealed class NegotiationState
{
    /// <summary>
    /// Gets all actions taken during negotiation.
    /// </summary>
    public ImmutableList<NegotiationAction> Actions { get; }

    /// <summary>
    /// Gets the current highest bid, or null if no announcements yet.
    /// </summary>
    public GameMode? CurrentBid { get; }

    /// <summary>
    /// Gets the player who made the current bid.
    /// </summary>
    public PlayerPosition? CurrentBidder { get; }

    /// <summary>
    /// Gets the position of the player whose turn it is.
    /// </summary>
    public PlayerPosition CurrentPlayer { get; }

    /// <summary>
    /// Gets the dealer position (used to determine first player).
    /// </summary>
    public PlayerPosition Dealer { get; }

    /// <summary>
    /// Gets whether negotiation has ended.
    /// </summary>
    public bool IsComplete { get; }

    /// <summary>
    /// Gets the number of consecutive accepts.
    /// </summary>
    public int ConsecutiveAccepts { get; }

    /// <summary>
    /// Tracks which modes have been doubled (mode -> first turn doubled).
    /// </summary>
    public ImmutableDictionary<GameMode, int> DoubledModes { get; }

    /// <summary>
    /// Tracks which modes have been redoubled.
    /// </summary>
    public ImmutableHashSet<GameMode> RedoubledModes { get; }

    /// <summary>
    /// Tracks which team has announced a Colour mode (restriction: one per team).
    /// </summary>
    public ImmutableDictionary<Team, GameMode> TeamColourAnnouncements { get; }

    /// <summary>
    /// Tracks which players have accepted (cannot announce after accepting).
    /// </summary>
    public ImmutableHashSet<PlayerPosition> PlayersWhoAccepted { get; }

    /// <summary>
    /// Gets whether a double has occurred (restricting further announcements).
    /// </summary>
    public bool HasDoubleOccurred { get; }

    private NegotiationState(
        ImmutableList<NegotiationAction> actions,
        GameMode? currentBid,
        PlayerPosition? currentBidder,
        PlayerPosition currentPlayer,
        PlayerPosition dealer,
        bool isComplete,
        int consecutiveAccepts,
        ImmutableDictionary<GameMode, int> doubledModes,
        ImmutableHashSet<GameMode> redoubledModes,
        ImmutableDictionary<Team, GameMode> teamColourAnnouncements,
        ImmutableHashSet<PlayerPosition> playersWhoAccepted,
        bool hasDoubleOccurred)
    {
        Actions = actions;
        CurrentBid = currentBid;
        CurrentBidder = currentBidder;
        CurrentPlayer = currentPlayer;
        Dealer = dealer;
        IsComplete = isComplete;
        ConsecutiveAccepts = consecutiveAccepts;
        DoubledModes = doubledModes;
        RedoubledModes = redoubledModes;
        TeamColourAnnouncements = teamColourAnnouncements;
        PlayersWhoAccepted = playersWhoAccepted;
        HasDoubleOccurred = hasDoubleOccurred;
    }

    /// <summary>
    /// Creates a new negotiation state starting with the player to dealer's left.
    /// </summary>
    public static NegotiationState Create(PlayerPosition dealer)
    {
        var firstPlayer = dealer.Next();
        return new NegotiationState(
            ImmutableList<NegotiationAction>.Empty,
            null,
            null,
            firstPlayer,
            dealer,
            false,
            0,
            ImmutableDictionary<GameMode, int>.Empty,
            ImmutableHashSet<GameMode>.Empty,
            ImmutableDictionary<Team, GameMode>.Empty,
            ImmutableHashSet<PlayerPosition>.Empty,
            false);
    }

    /// <summary>
    /// Applies an action and returns the new state.
    /// </summary>
    public NegotiationState Apply(NegotiationAction action)
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("Negotiation is already complete.");
        }

        if (action.Player != CurrentPlayer)
        {
            throw new InvalidOperationException(
                $"It is {CurrentPlayer}'s turn, not {action.Player}'s.");
        }

        return action switch
        {
            AnnouncementAction announce => ApplyAnnouncement(announce),
            AcceptAction accept => ApplyAccept(accept),
            DoubleAction doubleBid => ApplyDouble(doubleBid),
            RedoubleAction redouble => ApplyRedouble(redouble),
            _ => throw new ArgumentException($"Unknown action type: {action.GetType()}")
        };
    }

    private NegotiationState ApplyAnnouncement(AnnouncementAction action)
    {
        var newActions = Actions.Add(action);
        var team = action.Player.GetTeam();

        var newTeamColour = action.Mode.IsColourMode()
            ? TeamColourAnnouncements.SetItem(team, action.Mode)
            : TeamColourAnnouncements;

        return new NegotiationState(
            newActions,
            action.Mode,
            action.Player,
            CurrentPlayer.Next(),
            Dealer,
            false,
            0,
            DoubledModes,
            RedoubledModes,
            newTeamColour,
            PlayersWhoAccepted,
            HasDoubleOccurred);
    }

    private NegotiationState ApplyAccept(AcceptAction action)
    {
        var newActions = Actions.Add(action);
        var newConsecutiveAccepts = ConsecutiveAccepts + 1;
        var newPlayersAccepted = PlayersWhoAccepted.Add(action.Player);

        // Check for auto-double on SansAs or ColourClubs
        var accepterTeam = action.Player.GetTeam();
        var bidderTeam = CurrentBidder?.GetTeam();
        var isOpponentAccept = bidderTeam != null && accepterTeam != bidderTeam;
        var causesAutoDouble = CurrentBid.HasValue &&
                               CurrentBid.Value.AcceptCausesAutoDouble() &&
                               isOpponentAccept &&
                               !DoubledModes.ContainsKey(CurrentBid.Value);

        var newDoubledModes = causesAutoDouble
            ? DoubledModes.SetItem(CurrentBid!.Value, Actions.Count)
            : DoubledModes;

        var newHasDouble = HasDoubleOccurred || causesAutoDouble;

        // Negotiation ends after 3 consecutive accepts
        var complete = newConsecutiveAccepts >= 3 && CurrentBid.HasValue;

        return new NegotiationState(
            newActions,
            CurrentBid,
            CurrentBidder,
            complete ? CurrentPlayer : CurrentPlayer.Next(),
            Dealer,
            complete,
            newConsecutiveAccepts,
            newDoubledModes,
            RedoubledModes,
            TeamColourAnnouncements,
            newPlayersAccepted,
            newHasDouble);
    }

    private NegotiationState ApplyDouble(DoubleAction action)
    {
        var newActions = Actions.Add(action);
        var newDoubledModes = DoubledModes.SetItem(action.TargetMode, Actions.Count);

        return new NegotiationState(
            newActions,
            CurrentBid,
            CurrentBidder,
            CurrentPlayer.Next(),
            Dealer,
            false,
            0,
            newDoubledModes,
            RedoubledModes,
            TeamColourAnnouncements,
            PlayersWhoAccepted,
            true);
    }

    private NegotiationState ApplyRedouble(RedoubleAction action)
    {
        var newActions = Actions.Add(action);
        var newRedoubledModes = RedoubledModes.Add(action.TargetMode);

        return new NegotiationState(
            newActions,
            CurrentBid,
            CurrentBidder,
            CurrentPlayer.Next(),
            Dealer,
            false,
            0,
            DoubledModes,
            newRedoubledModes,
            TeamColourAnnouncements,
            PlayersWhoAccepted,
            true);
    }

    /// <summary>
    /// Resolves the final game mode after negotiation completes.
    /// Uses priority rule: first announced mode that was doubled.
    /// </summary>
    public (GameMode Mode, Team AnnouncerTeam, MultiplierState Multiplier) ResolveFinalMode()
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException("Negotiation is not complete.");
        }

        if (!CurrentBid.HasValue || !CurrentBidder.HasValue)
        {
            throw new InvalidOperationException("No bid was made.");
        }

        // If any mode was doubled, find the first announced mode that was doubled
        if (DoubledModes.Count > 0)
        {
            // Find all announcements in order
            var announcements = Actions
                .OfType<AnnouncementAction>()
                .ToList();

            // Find the first announced mode that was doubled
            foreach (var announcement in announcements)
            {
                if (DoubledModes.ContainsKey(announcement.Mode))
                {
                    var multiplier = RedoubledModes.Contains(announcement.Mode)
                        ? MultiplierState.Redoubled
                        : MultiplierState.Doubled;

                    return (announcement.Mode, announcement.Player.GetTeam(), multiplier);
                }
            }
        }

        // No doubles, use the current (highest) bid
        return (CurrentBid.Value, CurrentBidder.Value.GetTeam(), MultiplierState.Normal);
    }

    /// <summary>
    /// Gets the modes that can be announced by the current player.
    /// </summary>
    public IEnumerable<GameMode> GetAvailableModes()
    {
        if (IsComplete || HasDoubleOccurred) yield break;

        var playerTeam = CurrentPlayer.GetTeam();

        foreach (var mode in GameModeExtensions.GetAllModes())
        {
            // Must be higher than current bid
            if (CurrentBid.HasValue && !mode.IsHigherThan(CurrentBid.Value))
                continue;

            // Check Colour restriction: one per team
            if (mode.IsColourMode() && TeamColourAnnouncements.ContainsKey(playerTeam))
                continue;

            // Cannot announce if player has accepted
            if (PlayersWhoAccepted.Contains(CurrentPlayer))
                continue;

            yield return mode;
        }
    }
}
