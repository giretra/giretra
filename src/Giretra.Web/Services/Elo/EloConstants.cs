namespace Giretra.Web.Services.Elo;

public static class EloConstants
{
    public const double K_FACTOR = 32;
    public const double BOT_GATE_THRESHOLD = 4000.0;

    // Mixed-team multipliers: bot teammate
    public const double BOT_TEAMMATE_WIN_MULT = 1D;
    public const double BOT_TEAMMATE_LOSS_MULT = 1;

    // Mixed-team multipliers: 1 bot opponent
    public const double ONE_BOT_OPP_WIN_MULT = 1D;
    public const double ONE_BOT_OPP_LOSS_MULT = 1D;

    // Mixed-team multipliers: 2 bot opponents
    public const double TWO_BOT_OPP_WIN_MULT = 1D;
    public const double TWO_BOT_OPP_LOSS_MULT = 1D;

    // Weekly bot-Elo cap
    public const int MAX_BOT_ELO_PER_WEEK = 6000;

    // Abandonment
    public const int MAX_DECAY = 32;
    public const int OPPONENT_ABANDON_GAIN_CAP = 10;
}
