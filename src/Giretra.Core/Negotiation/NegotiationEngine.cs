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

        // Find the index of first announcement or accept by the current player (if any)
        // If the player has announced or accepted, they implicitly passed on doubling all earlier opponent bids
        int playerFirstPassIndex = -1;
        for (int i = 0; i < state.Actions.Count; i++)
        {
            if (state.Actions[i].Player == state.CurrentPlayer && state.Actions[i] is AnnouncementAction or AcceptAction)
            {
                playerFirstPassIndex = i;
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
                        // If player has announced or accepted, they can only double bids made AFTER that action
                        // (they passed on earlier bids)
                        if (playerFirstPassIndex == -1 || i > playerFirstPassIndex)
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

        // Redouble not allowed for this mode
        if (!mode.CanRedouble()) return false;

        // Mode must have been doubled
        if (!state.DoubledModes.ContainsKey(mode)) return false;

        // Mode must not already be redoubled
        if (state.RedoubledModes.Contains(mode)) return false;

        var playerTeam = state.CurrentPlayer.GetTeam();

        // Find who announced this mode
        var announcer = state.Actions
            .OfType<AnnouncementAction>()
            .FirstOrDefault(a => a.Mode == mode);

        if (announcer is null) return false;

        // Auto-doubled: opponent's partner (same team as auto-doubler) can redouble
        // Normal: announcer's team can redouble
        if (state.AutoDoubledModes.Contains(mode))
            return announcer.Player.GetTeam() != playerTeam;

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
    /// Checks if the current player can re-redouble the specified mode.
    /// </summary>
    public static bool CanReRedouble(NegotiationState state, GameMode mode)
    {
        if (state.IsComplete) return false;

        // Mode must have been redoubled
        if (!state.RedoubledModes.Contains(mode)) return false;

        // Mode must not already be re-redoubled
        if (state.ReRedoubledModes.Contains(mode)) return false;

        // Re-redouble only allowed for ColourClubs
        if (!mode.CanReRedouble()) return false;

        var playerTeam = state.CurrentPlayer.GetTeam();

        var announcer = state.Actions
            .OfType<AnnouncementAction>()
            .FirstOrDefault(a => a.Mode == mode);

        if (announcer is null) return false;

        // Auto-doubled: announcer's team can re-redouble (inverted)
        // Normal: opponent team can re-redouble
        if (state.AutoDoubledModes.Contains(mode))
            return announcer.Player.GetTeam() == playerTeam;

        return announcer.Player.GetTeam() != playerTeam;
    }

    /// <summary>
    /// Checks if the current player can re-redouble any mode.
    /// </summary>
    public static bool CanReRedouble(NegotiationState state, out IReadOnlyList<GameMode> reRedoubleableModes)
    {
        reRedoubleableModes = Array.Empty<GameMode>();

        if (state.IsComplete) return false;

        var modes = state.RedoubledModes
            .Where(m => CanReRedouble(state, m))
            .ToList();

        reRedoubleableModes = modes;
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

        // Re-redouble
        if (CanReRedouble(state, out var reRedoubleableModes))
        {
            foreach (var mode in reRedoubleableModes)
            {
                actions.Add(new ReRedoubleAction(player, mode));
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
            ReRedoubleAction reRedouble => ValidateReRedouble(state, reRedouble),
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

        // Check if player has announced or accepted - if so, they can only double bids made after that action
        int playerFirstPassIndex = -1;
        for (int i = 0; i < state.Actions.Count; i++)
        {
            if (state.Actions[i].Player == action.Player && state.Actions[i] is AnnouncementAction or AcceptAction)
            {
                playerFirstPassIndex = i;
                break;
            }
        }

        if (playerFirstPassIndex != -1 && targetModeIndex < playerFirstPassIndex)
        {
            return $"Cannot double {action.TargetMode}: you implicitly passed on this bid.";
        }

        return null;
    }

    private static string? ValidateRedouble(NegotiationState state, RedoubleAction action)
    {
        if (!action.TargetMode.CanRedouble())
        {
            return $"Cannot redouble {action.TargetMode}.";
        }

        if (!state.DoubledModes.ContainsKey(action.TargetMode))
        {
            return $"{action.TargetMode} has not been doubled.";
        }

        if (state.RedoubledModes.Contains(action.TargetMode))
        {
            return $"{action.TargetMode} has already been redoubled.";
        }

        var playerTeam = action.Player.GetTeam();

        var announcer = state.Actions
            .OfType<AnnouncementAction>()
            .FirstOrDefault(a => a.Mode == action.TargetMode);

        if (announcer is null)
        {
            return $"{action.TargetMode} has not been announced.";
        }

        // Auto-doubled: opponent's team (not announcer's team) can redouble
        // Normal: announcer's team can redouble
        if (state.AutoDoubledModes.Contains(action.TargetMode))
        {
            if (announcer.Player.GetTeam() == playerTeam)
            {
                return "Only the opponent team can redouble an auto-doubled mode.";
            }
        }
        else
        {
            if (announcer.Player.GetTeam() != playerTeam)
            {
                return "Only the announcer's team can redouble.";
            }
        }

        return null;
    }

    private static string? ValidateReRedouble(NegotiationState state, ReRedoubleAction action)
    {
        if (!state.RedoubledModes.Contains(action.TargetMode))
        {
            return $"{action.TargetMode} has not been redoubled.";
        }

        if (state.ReRedoubledModes.Contains(action.TargetMode))
        {
            return $"{action.TargetMode} has already been re-redoubled.";
        }

        if (!action.TargetMode.CanReRedouble())
        {
            return $"Cannot re-redouble {action.TargetMode}.";
        }

        var playerTeam = action.Player.GetTeam();

        var announcer = state.Actions
            .OfType<AnnouncementAction>()
            .FirstOrDefault(a => a.Mode == action.TargetMode);

        if (announcer is null)
        {
            return $"{action.TargetMode} has not been announced.";
        }

        // Auto-doubled: announcer's team can re-redouble (inverted)
        // Normal: opponent team can re-redouble
        if (state.AutoDoubledModes.Contains(action.TargetMode))
        {
            if (announcer.Player.GetTeam() != playerTeam)
            {
                return "Only the announcer's team can re-redouble an auto-doubled mode.";
            }
        }
        else
        {
            if (announcer.Player.GetTeam() == playerTeam)
            {
                return "Only the opponent team can re-redouble.";
            }
        }

        return null;
    }
}
