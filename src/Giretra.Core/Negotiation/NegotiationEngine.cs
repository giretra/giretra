using Giretra.Core.GameModes;
using Giretra.Core.Players;
using Giretra.Core.State;

namespace Giretra.Core.Negotiation;

/// <summary>
/// Engine for validating and applying negotiation actions.
/// </summary>
public static class NegotiationEngine
{
    /// <summary>
    /// Checks if the current player can announce the specified mode.
    /// </summary>
    public static bool CanAnnounce(NegotiationState state, GameMode mode)
    {
        if (state.IsComplete) return false;

        // Cannot announce after a double has occurred
        if (state.HasDoubleOccurred) return false;

        // Cannot announce if player has already accepted
        if (state.PlayersWhoAccepted.Contains(state.CurrentPlayer)) return false;

        // Must be higher than current bid (first player has no current bid)
        if (state.CurrentBid.HasValue && !mode.IsHigherThan(state.CurrentBid.Value))
        {
            return false;
        }

        // One Colour mode per team
        if (mode.IsColourMode())
        {
            var team = state.CurrentPlayer.GetTeam();
            if (state.TeamColourAnnouncements.ContainsKey(team))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks if the current player can accept.
    /// </summary>
    public static bool CanAccept(NegotiationState state)
    {
        if (state.IsComplete) return false;

        // First player must announce (cannot accept with no bid)
        if (!state.CurrentBid.HasValue) return false;

        return true;
    }

    /// <summary>
    /// Checks if the current player can double.
    /// </summary>
    public static bool CanDouble(NegotiationState state)
    {
        return CanDouble(state, out _);
    }

    /// <summary>
    /// Checks if the current player can double any mode, returning the modes that can be doubled.
    /// </summary>
    public static bool CanDouble(NegotiationState state, out IReadOnlyList<GameMode> doubleableModes)
    {
        doubleableModes = Array.Empty<GameMode>();

        if (state.IsComplete) return false;
        if (!state.CurrentBid.HasValue) return false;

        var playerTeam = state.CurrentPlayer.GetTeam();
        var modes = new List<GameMode>();

        // Find the index of first announcement by the current player (if any)
        // If the player has announced, they implicitly passed on all earlier opponent bids
        int playerFirstAnnouncementIndex = -1;
        for (int i = 0; i < state.Actions.Count; i++)
        {
            if (state.Actions[i] is AnnouncementAction a && a.Player == state.CurrentPlayer)
            {
                playerFirstAnnouncementIndex = i;
                break;
            }
        }

        // Find all opponent bids that haven't been doubled yet
        for (int i = 0; i < state.Actions.Count; i++)
        {
            if (state.Actions[i] is AnnouncementAction announcement)
            {
                // Can only double opponent's bids
                if (announcement.Player.GetTeam() != playerTeam)
                {
                    // Check if not already doubled
                    if (!state.DoubledModes.ContainsKey(announcement.Mode))
                    {
                        // If player has announced, they can only double bids made AFTER their announcement
                        // (they passed on earlier bids by announcing)
                        if (playerFirstAnnouncementIndex == -1 || i > playerFirstAnnouncementIndex)
                        {
                            modes.Add(announcement.Mode);
                        }
                    }
                }
            }
        }

        doubleableModes = modes;
        return modes.Count > 0;
    }

    /// <summary>
    /// Checks if the current player can redouble the specified mode.
    /// </summary>
    public static bool CanRedouble(NegotiationState state, GameMode mode)
    {
        if (state.IsComplete) return false;

        // Mode must have been doubled
        if (!state.DoubledModes.ContainsKey(mode)) return false;

        // Mode must not already be redoubled
        if (state.RedoubledModes.Contains(mode)) return false;

        // Redouble not allowed for NoTrumps or ColourClubs
        if (!mode.CanRedouble()) return false;

        // Only announcer's team can redouble
        var playerTeam = state.CurrentPlayer.GetTeam();

        // Find who announced this mode
        var announcer = state.Actions
            .OfType<AnnouncementAction>()
            .FirstOrDefault(a => a.Mode == mode);

        if (announcer is null) return false;

        return announcer.Player.GetTeam() == playerTeam;
    }

    /// <summary>
    /// Checks if the current player can redouble any mode.
    /// </summary>
    public static bool CanRedouble(NegotiationState state, out IReadOnlyList<GameMode> redoubleableModes)
    {
        redoubleableModes = Array.Empty<GameMode>();

        if (state.IsComplete) return false;

        var modes = state.DoubledModes.Keys
            .Where(m => CanRedouble(state, m))
            .ToList();

        redoubleableModes = modes;
        return modes.Count > 0;
    }

    /// <summary>
    /// Gets all valid actions for the current player.
    /// </summary>
    public static IReadOnlyList<NegotiationAction> GetValidActions(NegotiationState state)
    {
        var actions = new List<NegotiationAction>();
        var player = state.CurrentPlayer;

        // Announcements
        foreach (var mode in state.GetAvailableModes())
        {
            if (CanAnnounce(state, mode))
            {
                actions.Add(new AnnouncementAction(player, mode));
            }
        }

        // Accept
        if (CanAccept(state))
        {
            actions.Add(new AcceptAction(player));
        }

        // Double
        if (CanDouble(state, out var doubleableModes))
        {
            foreach (var mode in doubleableModes)
            {
                actions.Add(new DoubleAction(player, mode));
            }
        }

        // Redouble
        if (CanRedouble(state, out var redoubleableModes))
        {
            foreach (var mode in redoubleableModes)
            {
                actions.Add(new RedoubleAction(player, mode));
            }
        }

        return actions;
    }

    /// <summary>
    /// Validates an action and returns an error message if invalid, null if valid.
    /// </summary>
    public static string? ValidateAction(NegotiationState state, NegotiationAction action)
    {
        if (state.IsComplete)
        {
            return "Negotiation is already complete.";
        }

        if (action.Player != state.CurrentPlayer)
        {
            return $"It is {state.CurrentPlayer}'s turn, not {action.Player}'s.";
        }

        return action switch
        {
            AnnouncementAction announce => ValidateAnnouncement(state, announce),
            AcceptAction => ValidateAccept(state),
            DoubleAction doubleBid => ValidateDouble(state, doubleBid),
            RedoubleAction redouble => ValidateRedouble(state, redouble),
            _ => $"Unknown action type: {action.GetType().Name}"
        };
    }

    private static string? ValidateAnnouncement(NegotiationState state, AnnouncementAction action)
    {
        if (state.HasDoubleOccurred)
        {
            return "Cannot announce after a double has occurred.";
        }

        if (state.PlayersWhoAccepted.Contains(action.Player))
        {
            return "Cannot announce after accepting.";
        }

        if (state.CurrentBid.HasValue && !action.Mode.IsHigherThan(state.CurrentBid.Value))
        {
            return $"Must announce a mode higher than {state.CurrentBid.Value}.";
        }

        if (action.Mode.IsColourMode())
        {
            var team = action.Player.GetTeam();
            if (state.TeamColourAnnouncements.TryGetValue(team, out var existingMode))
            {
                return $"Team has already announced {existingMode}. Only one Colour per team per deal.";
            }
        }

        return null;
    }

    private static string? ValidateAccept(NegotiationState state)
    {
        if (!state.CurrentBid.HasValue)
        {
            return "Cannot accept when no bid has been made.";
        }

        return null;
    }

    private static string? ValidateDouble(NegotiationState state, DoubleAction action)
    {
        if (!state.CurrentBid.HasValue)
        {
            return "Cannot double when no bid has been made.";
        }

        var playerTeam = action.Player.GetTeam();

        // Find who announced this mode and its index
        int targetModeIndex = -1;
        AnnouncementAction? announcer = null;
        for (int i = 0; i < state.Actions.Count; i++)
        {
            if (state.Actions[i] is AnnouncementAction a && a.Mode == action.TargetMode)
            {
                announcer = a;
                targetModeIndex = i;
                break;
            }
        }

        if (announcer is null)
        {
            return $"{action.TargetMode} has not been announced.";
        }

        if (announcer.Player.GetTeam() == playerTeam)
        {
            return "Cannot double your own team's bid.";
        }

        if (state.DoubledModes.ContainsKey(action.TargetMode))
        {
            return $"{action.TargetMode} has already been doubled.";
        }

        // Check if player has announced - if so, they can only double bids made after their announcement
        int playerFirstAnnouncementIndex = -1;
        for (int i = 0; i < state.Actions.Count; i++)
        {
            if (state.Actions[i] is AnnouncementAction a && a.Player == action.Player)
            {
                playerFirstAnnouncementIndex = i;
                break;
            }
        }

        if (playerFirstAnnouncementIndex != -1 && targetModeIndex < playerFirstAnnouncementIndex)
        {
            return $"Cannot double {action.TargetMode}: you passed on this bid when you announced.";
        }

        return null;
    }

    private static string? ValidateRedouble(NegotiationState state, RedoubleAction action)
    {
        if (!state.DoubledModes.ContainsKey(action.TargetMode))
        {
            return $"{action.TargetMode} has not been doubled.";
        }

        if (state.RedoubledModes.Contains(action.TargetMode))
        {
            return $"{action.TargetMode} has already been redoubled.";
        }

        if (!action.TargetMode.CanRedouble())
        {
            return $"Cannot redouble {action.TargetMode}.";
        }

        var playerTeam = action.Player.GetTeam();

        var announcer = state.Actions
            .OfType<AnnouncementAction>()
            .FirstOrDefault(a => a.Mode == action.TargetMode);

        if (announcer is null)
        {
            return $"{action.TargetMode} has not been announced.";
        }

        if (announcer.Player.GetTeam() != playerTeam)
        {
            return "Only the announcer's team can redouble.";
        }

        return null;
    }
}
