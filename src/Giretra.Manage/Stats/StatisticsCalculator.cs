namespace Giretra.Manage.Stats;

/// <summary>
/// Statistical calculations for benchmark analysis.
/// </summary>
public static class StatisticsCalculator
{
    /// <summary>
    /// Calculates the 95% confidence interval for a proportion using the Wilson score interval.
    /// </summary>
    /// <param name="successes">Number of successes (wins).</param>
    /// <param name="total">Total number of trials (matches).</param>
    /// <returns>Tuple of (lower bound, upper bound) as proportions (0-1).</returns>
    public static (double Lower, double Upper) WilsonConfidenceInterval(int successes, int total)
    {
        if (total == 0) return (0, 0);

        // Z-score for 95% confidence
        const double z = 1.96;
        double n = total;
        double p = (double)successes / total;

        double denominator = 1 + z * z / n;
        double center = p + z * z / (2 * n);
        double margin = z * Math.Sqrt((p * (1 - p) + z * z / (4 * n)) / n);

        double lower = (center - margin) / denominator;
        double upper = (center + margin) / denominator;

        return (Math.Max(0, lower), Math.Min(1, upper));
    }

    /// <summary>
    /// Performs a two-tailed binomial test to determine if the observed win rate
    /// is significantly different from 50%.
    /// </summary>
    /// <param name="successes">Number of successes (wins for one team).</param>
    /// <param name="total">Total number of trials (matches).</param>
    /// <returns>The p-value (probability of observing this result or more extreme under null hypothesis).</returns>
    public static double BinomialTestPValue(int successes, int total)
    {
        if (total == 0) return 1.0;

        // Use normal approximation for large samples (n >= 30)
        // H0: p = 0.5
        double p0 = 0.5;
        double observed = (double)successes / total;
        double standardError = Math.Sqrt(p0 * (1 - p0) / total);

        if (standardError == 0) return 1.0;

        double zScore = (observed - p0) / standardError;

        // Two-tailed p-value using normal CDF approximation
        double pValue = 2 * (1 - NormalCdf(Math.Abs(zScore)));

        return pValue;
    }

    /// <summary>
    /// Determines if the result is statistically significant at the given alpha level.
    /// </summary>
    /// <param name="pValue">The p-value from a statistical test.</param>
    /// <param name="alpha">Significance level (default 0.05 for 95% confidence).</param>
    /// <returns>True if the result is statistically significant.</returns>
    public static bool IsSignificant(double pValue, double alpha = 0.05)
    {
        return pValue < alpha;
    }

    /// <summary>
    /// Returns a human-readable interpretation of the p-value.
    /// </summary>
    public static string InterpretPValue(double pValue)
    {
        return pValue switch
        {
            < 0.001 => "Highly significant (p < 0.001)",
            < 0.01 => "Very significant (p < 0.01)",
            < 0.05 => "Significant (p < 0.05)",
            < 0.1 => "Marginally significant (p < 0.1)",
            _ => "Not significant (p >= 0.1)"
        };
    }

    /// <summary>
    /// Approximation of the standard normal cumulative distribution function.
    /// Uses the Abramowitz and Stegun approximation.
    /// </summary>
    private static double NormalCdf(double x)
    {
        // Constants for approximation
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;
        const double p = 0.3275911;

        // Save the sign of x
        int sign = x < 0 ? -1 : 1;
        x = Math.Abs(x) / Math.Sqrt(2);

        // Approximation
        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return 0.5 * (1.0 + sign * y);
    }
}
