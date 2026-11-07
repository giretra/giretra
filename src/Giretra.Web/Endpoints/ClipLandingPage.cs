using System.Net;
using System.Text;
using Giretra.Web.Models.Responses;
using Giretra.Web.Services.Clips;

namespace Giretra.Web.Endpoints;

/// <summary>
/// Server-rendered HTML of a clip's public page. Scrapers (WhatsApp, Facebook, X, Telegram)
/// read the Open Graph tags from here, so this must not depend on the Angular bundle.
/// Pure function of the clip: unit-testable.
/// </summary>
public static class ClipLandingPage
{
    public const int VideoWidth = 1080;
    public const int VideoHeight = 1920;

    public static string Render(ClipResponse clip, PublicOrigin origin)
    {
        var locale = ClipTexts.Normalize(clip.Locale);
        var title = Html(clip.Title);
        var description = Html(ClipTexts.Description(locale));
        var pageUrl = Html(clip.PageUrl);
        var ready = clip.Status == ClipStatusResponse.Ready;
        var removed = clip.Status == ClipStatusResponse.Removed;
        var failed = clip.Status == ClipStatusResponse.Failed;
        var poster = clip.PosterUrl != null ? Html(clip.PosterUrl) : Html(origin.Url("/og-image.png"));
        var video = clip.VideoUrl != null ? Html(clip.VideoUrl) : null;
        var replayUrl = Html(origin.Url($"/replay/clip/{clip.ShortId}"));
        var playUrl = Html(origin.Url("/?utm_source=clip&utm_medium=share&utm_campaign=" + clip.ShortId));

        var sb = new StringBuilder(4096);
        sb.Append("<!DOCTYPE html>\n<html lang=\"").Append(locale).Append("\">\n<head>\n");
        sb.Append("<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        sb.Append("<title>").Append(title).Append(" · Giretra</title>\n");
        sb.Append("<meta name=\"description\" content=\"").Append(description).Append("\">\n");
        sb.Append("<link rel=\"icon\" href=\"/favicon.svg\" type=\"image/svg+xml\">\n");
        sb.Append("<link rel=\"canonical\" href=\"").Append(pageUrl).Append("\">\n");

        if (!ready)
            sb.Append("<meta name=\"robots\" content=\"noindex\">\n");

        sb.Append("<meta property=\"og:site_name\" content=\"Giretra\">\n");
        sb.Append("<meta property=\"og:title\" content=\"").Append(title).Append("\">\n");
        sb.Append("<meta property=\"og:description\" content=\"").Append(description).Append("\">\n");
        sb.Append("<meta property=\"og:url\" content=\"").Append(pageUrl).Append("\">\n");
        sb.Append("<meta property=\"og:locale\" content=\"").Append(OgLocale(locale)).Append("\">\n");
        sb.Append("<meta property=\"og:image\" content=\"").Append(poster).Append("\">\n");
        if (clip.PosterUrl != null)
        {
            sb.Append("<meta property=\"og:image:width\" content=\"").Append(VideoWidth).Append("\">\n");
            sb.Append("<meta property=\"og:image:height\" content=\"").Append(VideoHeight).Append("\">\n");
        }
        if (video != null)
        {
            sb.Append("<meta property=\"og:type\" content=\"video.other\">\n");
            sb.Append("<meta property=\"og:video\" content=\"").Append(video).Append("\">\n");
            sb.Append("<meta property=\"og:video:secure_url\" content=\"").Append(video).Append("\">\n");
            sb.Append("<meta property=\"og:video:type\" content=\"video/mp4\">\n");
            sb.Append("<meta property=\"og:video:width\" content=\"").Append(VideoWidth).Append("\">\n");
            sb.Append("<meta property=\"og:video:height\" content=\"").Append(VideoHeight).Append("\">\n");
            if (clip.DurationMs is int ms)
                sb.Append("<meta property=\"video:duration\" content=\"").Append((ms + 999) / 1000).Append("\">\n");
            sb.Append("<meta name=\"twitter:card\" content=\"player\">\n");
            sb.Append("<meta name=\"twitter:player\" content=\"").Append(replayUrl).Append("\">\n");
            sb.Append("<meta name=\"twitter:player:width\" content=\"540\">\n<meta name=\"twitter:player:height\" content=\"960\">\n");
        }
        else
        {
            sb.Append("<meta property=\"og:type\" content=\"website\">\n");
            sb.Append("<meta name=\"twitter:card\" content=\"summary_large_image\">\n");
        }
        sb.Append("<meta name=\"twitter:title\" content=\"").Append(title).Append("\">\n");
        sb.Append("<meta name=\"twitter:description\" content=\"").Append(description).Append("\">\n");
        sb.Append("<meta name=\"twitter:image\" content=\"").Append(poster).Append("\">\n");
        if (!ready && !removed && !failed)
            sb.Append("<meta http-equiv=\"refresh\" content=\"6\">\n");

        sb.Append("<style>").Append(Css).Append("</style>\n</head>\n<body>\n<main class=\"page\">\n");
        sb.Append("<a class=\"brand\" href=\"").Append(playUrl).Append("\">giretra</a>\n");
        sb.Append("<h1>").Append(title).Append("</h1>\n");

        if (removed)
        {
            sb.Append("<p class=\"note\">").Append(Html(ClipTexts.Removed(locale))).Append("</p>\n");
        }
        else if (failed)
        {
            sb.Append("<p class=\"note\">").Append(Html(ClipTexts.Failed(locale))).Append("</p>\n");
        }
        else if (video != null)
        {
            sb.Append("<video class=\"player\" controls playsinline preload=\"metadata\" poster=\"").Append(poster).Append("\">\n");
            sb.Append("<source src=\"").Append(video).Append("\" type=\"video/mp4\">\n</video>\n");
            sb.Append("<div class=\"actions\">\n");
            sb.Append("<a class=\"btn primary\" href=\"").Append(video).Append("?download=1\" download>").Append(Html(ClipTexts.Download(locale))).Append("</a>\n");
            sb.Append("<button class=\"btn\" type=\"button\" id=\"share\" hidden>").Append(Html(ClipTexts.Share(locale))).Append("</button>\n");
            sb.Append("</div>\n");
        }
        else if (clip.PosterUrl != null)
        {
            sb.Append("<img class=\"player\" src=\"").Append(poster).Append("\" alt=\"\">\n");
            sb.Append("<p class=\"note\">").Append(Html(ClipTexts.Preparing(locale))).Append("</p>\n");
        }
        else
        {
            sb.Append("<p class=\"note\">").Append(Html(ClipTexts.Preparing(locale))).Append("</p>\n");
        }

        if (!removed)
            sb.Append("<a class=\"link\" href=\"").Append(replayUrl).Append("\">").Append(Html(ClipTexts.WatchInteractive(locale))).Append("</a>\n");
        sb.Append("<a class=\"btn cta\" href=\"").Append(playUrl).Append("\">").Append(Html(ClipTexts.PlayCta(locale))).Append("</a>\n");
        sb.Append("</main>\n");
        if (video != null)
            sb.Append(ShareScript(title, pageUrl, video));
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private static string OgLocale(string locale) => locale switch { "fr" => "fr_FR", "mg" => "mg_MG", _ => "en_US" };

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string ShareScript(string title, string pageUrl, string videoUrl) =>
        "<script>\n(function(){var b=document.getElementById('share');if(!b||!navigator.share)return;b.hidden=false;" +
        "b.addEventListener('click',async function(){try{" +
        "if(navigator.canShare){var r=await fetch('" + videoUrl + "');var f=new File([await r.blob()],'giretra.mp4',{type:'video/mp4'});" +
        "if(navigator.canShare({files:[f]})){await navigator.share({files:[f],title:'" + JsString(title) + "',url:'" + pageUrl + "'});return;}}" +
        "await navigator.share({title:'" + JsString(title) + "',url:'" + pageUrl + "'});}catch(e){}});})();\n</script>\n";

    private static string JsString(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("<", "\\u003c");

    private const string Css =
        "html,body{margin:0;background:#141922;color:#e6e9ef;font-family:Inter,system-ui,-apple-system,sans-serif}" +
        ".page{max-width:540px;margin:0 auto;padding:1.25rem 1rem 3rem;display:flex;flex-direction:column;align-items:center;gap:1rem;text-align:center}" +
        ".brand{align-self:flex-start;font-weight:800;letter-spacing:.05em;color:#f0b429;text-decoration:none}" +
        "h1{font-size:1.1rem;font-weight:600;margin:0}" +
        ".player{width:100%;max-height:80vh;border-radius:16px;background:#000;object-fit:contain}" +
        ".actions{display:flex;gap:.75rem;flex-wrap:wrap;justify-content:center}" +
        ".btn{display:inline-block;padding:.7rem 1.2rem;border-radius:9999px;border:1px solid #39414f;color:inherit;background:#1d2430;text-decoration:none;font-weight:600;cursor:pointer;font:inherit}" +
        ".btn.primary{background:#f0b429;color:#141922;border-color:transparent}" +
        ".btn.cta{margin-top:1rem;background:#2f7bd6;border-color:transparent;color:#fff}" +
        ".link{color:#9fb3c8}.note{color:#9fb3c8}";
}
