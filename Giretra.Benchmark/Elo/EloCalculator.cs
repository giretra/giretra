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
    /// Calculates new ratings for both players after a match.
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
        var expected1 = ExpectedScore(player1Rating, player2Rating);
        var expected2 = ExpectedScore(player2Rating, player1Rating);

        var actual1 = player1Won ? 1.0 : 0.0;
        var actual2 = player1Won ? 0.0 : 1.0;

        var newRating1 = NewRating(player1Rating, expected1, actual1, kFactor);
        var newRating2 = NewRating(player2Rating, expected2, actual2, kFactor);

        return (newRating1, newRating2);
    }
}
