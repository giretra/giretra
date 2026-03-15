using Giretra.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Web.Controllers;

[ApiController]
[Route("api/avatars")]
public class AvatarsController : ControllerBase
{
    [HttpGet("{userId:guid}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult> GetAvatar(Guid userId, [FromServices] GiretraDbContext? db = null)
    {
        if (db == null)
            return NotFound();

        var blob = await db.BlobStore
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Key == $"avatar/{userId}");

        if (blob == null)
            return NotFound();

        return File(blob.Data, blob.ContentType);
    }
}
