namespace Giretra.Benchmark.Elo;

/// <summary>
/// Calculates ELO rating changes using the standard ELO formula.
/// </summary>
public static class EloCalculator
{
    /// <summary>
    /// Calculates the expected score for a player given both ratings.
    /// </summary>
    /// <param name="playerRating">The player's current rating.</param>
    /// <param name="opponentRating">The opponent's current rating.</param>
    /// <returns>Expected score between 0 and 1.</returns>
    public static double ExpectedScore(double playerRating, double opponentRating)
    {
        return 1.0 / (1.0 + Math.Pow(10, (opponentRating - playerRating) / 400.0));
    }

    /// <summary>
    /// Calculates the new rating for a player after a match.
    /// </summary>
    /// <param name="currentRating">The player's current rating.</param>
    /// <param name="expectedScore">The expected score (from ExpectedScore).</param>
    /// <param name="actualScore">The actual score (1.0 for win, 0.0 for loss).</param>
    /// <param name="kFactor">The K-factor (sensitivity of rating changes).</param>
    /// <returns>The new rating.</returns>
    public static double NewRating(double currentRating, double expectedScore, double actualScore, double kFactor)
    {
        return currentRating + kFactor * (actualScore - expectedScore);
    }

    /// <summary>
    /// Calculates new ratings for both players after a match using binary win/loss.
    /// </summary>
    /// <param name="player1Rating">Player 1's current rating.</param>
    /// <param name="player2Rating">Player 2's current rating.</param>
    /// <param name="player1Won">True if player 1 won, false if player 2 won.</param>
    /// <param name="kFactor">The K-factor.</param>
    /// <returns>Tuple of (new player 1 rating, new player 2 rating).</returns>
    public static (double NewPlayer1Rating, double NewPlayer2Rating) CalculateNewRatings(
        double player1Rating,
        double player2Rating,
        bool player1Won,
        double kFactor)
    {
        var actual1 = player1Won ? 1.0 : 0.0;
        var actual2 = player1Won ? 0.0 : 1.0;

        return CalculateNewRatings(player1Rating, player2Rating, actual1, actual2, kFactor, kFactor);
    }

    /// <summary>
    /// Calculates new ratings for both players using continuous actual scores and per-player K-factors.
    /// </summary>
    /// <param name="player1Rating">Player 1's current rating.</param>
    /// <param name="player2Rating">Player 2's current rating.</param>
    /// <param name="actualScore1">Player 1's actual score (0.0 to 1.0).</param>
    /// <param name="actualScore2">Player 2's actual score (0.0 to 1.0).</param>
    /// <param name="kFactor1">K-factor for player 1.</param>
    /// <param name="kFactor2">K-factor for player 2.</param>
    /// <returns>Tuple of (new player 1 rating, new player 2 rating).</returns>
    public static (double NewPlayer1Rating, double NewPlayer2Rating) CalculateNewRatings(
        double player1Rating,
        double player2Rating,
        double actualScore1,
        double actualScore2,
        double kFactor1,
        double kFactor2)
    {
        var expected1 = ExpectedScore(player1Rating, player2Rating);
        var expected2 = ExpectedScore(player2Rating, player1Rating);

        var newRating1 = NewRating(player1Rating, expected1, actualScore1, kFactor1);
        var newRating2 = NewRating(player2Rating, expected2, actualScore2, kFactor2);

        return (newRating1, newRating2);
    }

    /// <summary>
    /// Computes a decaying K-factor based on how many matches a player has completed.
    /// Starts at kMax and decays toward kMin with the given half-life.
    /// </summary>
    /// <param name="kMax">Initial (maximum) K-factor for rapid early convergence.</param>
    /// <param name="kMin">Floor (minimum) K-factor for late-game stability.</param>
    /// <param name="matchesPlayed">Number of matches the player has completed.</param>
    /// <param name="halfLife">Number of matches at which K is halfway between kMax and kMin.</param>
    /// <returns>The effective K-factor for the player.</returns>
    public static double EffectiveKFactor(double kMax, double kMin, int matchesPlayed, double halfLife)
    {
        return kMin + (kMax - kMin) / (1.0 + matchesPlayed / halfLife);
    }

    /// <summary>
    /// Computes a margin-based actual score (0.5 to 1.0) for the winner.
    /// A blowout yields ~1.0, a close win yields ~0.5.
    /// </summary>
    /// <param name="winnerScore">The winning team's final score.</param>
    /// <param name="loserScore">The losing team's final score.</param>
    /// <param name="targetScore">The target score to win a match.</param>
    /// <returns>Actual score for the winner (0.5 to 1.0).</returns>
    public static double MarginScore(int winnerScore, int loserScore, int targetScore)
    {
        var margin = (double)(winnerScore - loserScore) / targetScore;
        return 0.5 + 0.5 * Math.Clamp(margin, 0.0, 1.0);
    }
}
