namespace Giretra.Web.Services.Elo;

public sealed class EloCalculationService
{
    public EloResult ComputeNormalMatchDelta(PlayerContext ctx)
    {
        // Bots always get zero delta
        if (ctx.IsBot)
            return new EloResult(ctx.PlayerId, ctx.CurrentElo, ctx.CurrentElo, 0);

        var expected = 1.0 / (1.0 + Math.Pow(10.0, (ctx.OpponentCompositeElo - ctx.CurrentElo) / 400.0));
        var result = ctx.IsWinner ? 1.0 : 0.0;
        var delta = EloConstants.K_FACTOR * (result - expected);

        // Bot-gate suppression: only on wins when bots are involved
        if (ctx.IsWinner && ctx.InvolvedBots)
        {
            var gap = Math.Max(0.0, ctx.CurrentElo - ctx.OpponentCompositeElo);
            var gateMultiplier = Math.Max(0.0, 1.0 - gap / EloConstants.BOT_GATE_THRESHOLD);
            delta *= gateMultiplier;
        }

        // Apply mixed-team multipliers (stacking: teammate mult × opponent mult)
        delta *= GetTeammateMultiplier(ctx.HasBotTeammate, ctx.IsWinner);
        delta *= GetOpponentMultiplier(ctx.BotOpponentCount, ctx.IsWinner);

        // Weekly bot-Elo cap: only on wins when bots are involved
        if (ctx.IsWinner && ctx.InvolvedBots)
        {
            var remaining = EloConstants.MAX_BOT_ELO_PER_WEEK - ctx.WeeklyBotEloGained;
            if (remaining <= 0)
                delta = 0;
            else
                delta = Math.Min(delta, remaining);
        }

        var change = (int)Math.Round(delta);
        return new EloResult(ctx.PlayerId, ctx.CurrentElo, ctx.CurrentElo + change, change);
    }

    public EloResult ComputeAbandonDelta(Guid playerId, int currentElo, AbandonRole role)
    {
        var change = role switch
        {
            AbandonRole.Abandoner => -EloConstants.MAX_DECAY,
            AbandonRole.Opponent => Math.Min(EloConstants.MAX_DECAY / 2, EloConstants.OPPONENT_ABANDON_GAIN_CAP),
            AbandonRole.TeammateOfAbandoner => 0,
            _ => 0
        };

        return new EloResult(playerId, currentElo, currentElo + change, change);
    }

    private static double GetTeammateMultiplier(bool hasBotTeammate, bool isWinner)
    {
        if (!hasBotTeammate)
            return 1.0;

        return isWinner ? EloConstants.BOT_TEAMMATE_WIN_MULT : EloConstants.BOT_TEAMMATE_LOSS_MULT;
    }

    private static double GetOpponentMultiplier(int botOpponentCount, bool isWinner)
    {
        return botOpponentCount switch
        {
            0 => 1.0,
            1 => isWinner ? EloConstants.ONE_BOT_OPP_WIN_MULT : EloConstants.ONE_BOT_OPP_LOSS_MULT,
            _ => isWinner ? EloConstants.TWO_BOT_OPP_WIN_MULT : EloConstants.TWO_BOT_OPP_LOSS_MULT
        };
    }
}

public sealed record EloResult(Guid PlayerId, int EloBefore, int EloAfter, int EloChange);

public sealed record PlayerContext(
    Guid PlayerId,
    int CurrentElo,
    bool IsBot,
    bool IsWinner,
    double OpponentCompositeElo,
    bool InvolvedBots,
    bool HasBotTeammate,
    int BotOpponentCount,
    int WeeklyBotEloGained
);

public enum AbandonRole
{
    Abandoner,
    Opponent,
    TeammateOfAbandoner
}
