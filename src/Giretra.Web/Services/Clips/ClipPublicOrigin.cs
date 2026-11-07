using Microsoft.Extensions.Options;

namespace Giretra.Web.Services.Clips;

public static class ClipPublicOrigin
{
    /// <summary>Configured public origin, else the request's scheme and host.</summary>
    public static PublicOrigin Resolve(HttpContext context)
    {
        var configured = context.RequestServices.GetRequiredService<IOptions<ClipOptions>>().Value.PublicBaseUrl;
        if (!string.IsNullOrWhiteSpace(configured))
            return new PublicOrigin(configured.TrimEnd('/'));
        var request = context.Request;
        return new PublicOrigin($"{request.Scheme}://{request.Host}");
    }
}
