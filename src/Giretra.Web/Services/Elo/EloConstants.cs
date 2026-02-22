namespace Giretra.Web.Services.Elo;

public static class EloConstants
{
    public const double K_FACTOR = 32;
    public const double BOT_GATE_THRESHOLD = 400.0;

    // Mixed-team multipliers: bot teammate
    public const double BOT_TEAMMATE_WIN_MULT = 0.70;
    public const double BOT_TEAMMATE_LOSS_MULT = 0.85;

    // Mixed-team multipliers: 1 bot opponent
    public const double ONE_BOT_OPP_WIN_MULT = 0.85;
    public const double ONE_BOT_OPP_LOSS_MULT = 1.15;

    // Mixed-team multipliers: 2 bot opponents
    public const double TWO_BOT_OPP_WIN_MULT = 0.70;
    public const double TWO_BOT_OPP_LOSS_MULT = 1.30;

    // Weekly bot-Elo cap
    public const int MAX_BOT_ELO_PER_WEEK = 50;

    // Abandonment
    public const int MAX_DECAY = 32;
    public const int OPPONENT_ABANDON_GAIN_CAP = 10;
}
