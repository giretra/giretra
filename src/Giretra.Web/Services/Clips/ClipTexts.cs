using Giretra.Core.GameModes;
using Giretra.Core.Players;

namespace Giretra.Web.Services.Clips;

/// <summary>
/// The few server-side strings of a clip (title, landing page) in the three UI languages.
/// Values mirror the Angular i18n files so a clip reads the same everywhere.
/// </summary>
public static class ClipTexts
{
    public static readonly string[] Locales = ["en", "fr", "mg"];

    public static string Normalize(string? locale)
        => locale != null && Locales.Contains(locale.ToLowerInvariant()) ? locale.ToLowerInvariant() : "en";

    public static string Mode(GameMode mode, string locale) => (mode, locale) switch
    {
        (GameMode.ColourClubs, "fr") => "Trèfle", (GameMode.ColourClubs, "mg") => "Trefla", (GameMode.ColourClubs, _) => "Clubs",
        (GameMode.ColourDiamonds, "fr") => "Carreau", (GameMode.ColourDiamonds, "mg") => "Karo", (GameMode.ColourDiamonds, _) => "Diamonds",
        (GameMode.ColourHearts, "fr") => "Cœur", (GameMode.ColourHearts, "mg") => "Kera", (GameMode.ColourHearts, _) => "Hearts",
        (GameMode.ColourSpades, "fr") => "Pique", (GameMode.ColourSpades, "mg") => "Pika", (GameMode.ColourSpades, _) => "Spades",
        (GameMode.NoTrumps, "fr") => "Sans Atout", (GameMode.NoTrumps, "mg") => "Sans A", (GameMode.NoTrumps, _) => "No Trumps",
        (GameMode.AllTrumps, "fr") => "Tout Atout", (GameMode.AllTrumps, "mg") => "Tout A", _ => "All Trumps",
    };

    /// <summary>Placeholder for a player who opted out of appearing by name.</summary>
    public static string HiddenPlayer(PlayerPosition position, string locale) => locale switch
    {
        "fr" => "Joueur " + SeatLetter(position),
        "mg" => "Mpilalao " + SeatLetter(position),
        _ => "Player " + SeatLetter(position),
    };

    private static string SeatLetter(PlayerPosition position) => position switch
    {
        PlayerPosition.Bottom => "S",
        PlayerPosition.Left => "W",
        PlayerPosition.Top => "N",
        _ => "E",
    };

    public static string Title(GameMode mode, int dealNumber, int fromTrick, int toTrick, string team1, string team2, string locale)
    {
        var tricks = fromTrick == toTrick
            ? locale switch { "fr" => $"pli {fromTrick}", "mg" => $"pli {fromTrick}", _ => $"trick {fromTrick}" }
            : locale switch { "fr" => $"plis {fromTrick}–{toTrick}", "mg" => $"pli {fromTrick}–{toTrick}", _ => $"tricks {fromTrick}–{toTrick}" };
        var deal = locale switch { "fr" => $"Donne {dealNumber}", "mg" => $"Fizarana {dealNumber}", _ => $"Deal {dealNumber}" };
        var vs = locale switch { "fr" => "contre", "mg" => "vs", _ => "vs" };
        return $"{Mode(mode, locale)} · {deal}, {tricks} · {team1} {vs} {team2}";
    }

    public static string Description(string locale) => locale switch
    {
        "fr" => "Un pli de Giretra, la belote malgache en ligne. Joue gratuitement sur play.giretra.com.",
        "mg" => "Pli iray avy amin'ny Giretra, belote malagasy an-tserasera. Milalaova maimaim-poana ao amin'ny play.giretra.com.",
        _ => "A trick from Giretra, the Malagasy belote online. Play for free at play.giretra.com.",
    };

    public static string Preparing(string locale) => locale switch
    {
        "fr" => "La vidéo est en cours de préparation…",
        "mg" => "Eo am-panomanana ny horonan-tsary…",
        _ => "The video is being prepared…",
    };

    public static string Removed(string locale) => locale switch
    {
        "fr" => "Ce clip n'est plus disponible.",
        "mg" => "Tsy misy intsony ity clip ity.",
        _ => "This clip is no longer available.",
    };

    public static string Download(string locale) => locale switch { "fr" => "Télécharger la vidéo", "mg" => "Alaivo ny horonan-tsary", _ => "Download video" };
    public static string WatchInteractive(string locale) => locale switch { "fr" => "Revoir le pli en interactif", "mg" => "Jereo indray", _ => "Open interactive replay" };
    public static string PlayCta(string locale) => locale switch { "fr" => "Jouer à Giretra", "mg" => "Milalaova Giretra", _ => "Play Giretra" };
    public static string Share(string locale) => locale switch { "fr" => "Partager", "mg" => "Zarao", _ => "Share" };
}
