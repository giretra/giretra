using System.Text.RegularExpressions;

namespace Giretra.Web.Validation;

public static partial class DisplayNameValidator
{
    private const int MinLength = 3;
    private const int MaxLength = 100;

    // Allowed: letters, digits, spaces, hyphens, underscores, periods, emojis (So, Sk)
    [GeneratedRegex(@"^[\p{L}\p{N}\s\-_.\p{So}\p{Sk}]+$")]
    private static partial Regex AllowedCharsRegex();

    [GeneratedRegex(@"\p{L}|\p{N}")]
    private static partial Regex HasLetterOrDigitRegex();

    [GeneratedRegex(@"  ")]
    private static partial Regex ConsecutiveSpacesRegex();

    public static (bool IsValid, string? Error) Validate(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return (false, "Display name is required.");

        var trimmed = displayName.Trim();

        if (trimmed.Length < MinLength)
            return (false, $"Display name must be at least {MinLength} characters.");

        if (trimmed.Length > MaxLength)
            return (false, $"Display name must be at most {MaxLength} characters.");

        if (!AllowedCharsRegex().IsMatch(trimmed))
            return (false, "Display name contains invalid characters. Only letters, digits, spaces, hyphens, underscores, periods, and emojis are allowed.");

        if (!HasLetterOrDigitRegex().IsMatch(trimmed))
            return (false, "Display name must contain at least one letter or digit.");

        if (ConsecutiveSpacesRegex().IsMatch(trimmed))
            return (false, "Display name cannot contain consecutive spaces.");

        return (true, null);
    }

    public static string Trim(string displayName) => displayName.Trim();
}
