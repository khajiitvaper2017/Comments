using Comments.Infrastructure.Options;
using Comments.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Comments.Api.Controllers;

[ApiController]
[Route("api/attachments")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class AttachmentsController(
    CommentsDbContext db,
    IOptions<StorageOptions> storage,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var a = await db.Attachments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return NotFound();

        var path = a.StorageReference;
        if (!System.IO.File.Exists(path))
        {
            var migratedPath = Path.GetFullPath(Path.Combine(
                environment.ContentRootPath, storage.Value.Root, a.StoredName));
            if (!System.IO.File.Exists(migratedPath)) return NotFound();

            a.StorageReference = migratedPath;
            await db.SaveChangesAsync(ct);
            path = migratedPath;
        }

        if (a.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return PhysicalFile(path, a.ContentType, true);

        return PhysicalFile(path, a.ContentType, a.OriginalName, true);
    }
}
