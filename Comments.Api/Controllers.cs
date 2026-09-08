using Comments.Application;
using Comments.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Comments.Api;

[ApiController]
[Route("api/captcha")]
public sealed class CaptchaController(ICaptchaService service) : ControllerBase
{
    [HttpGet]
    public CaptchaDto Get()
    {
        return service.Create();
    }
}

[ApiController]
[Route("api/comments")]
public sealed class CommentsController(ICommentService service) : ControllerBase
{
    [HttpGet]
    public Task<CommentPage> Get([FromQuery] int page = 1, [FromQuery] string sort = "createdAt",
        [FromQuery] bool descending = true, CancellationToken ct = default)
    {
        return service.GetRootsAsync(page, sort, descending, ct);
    }

    [HttpPost]
    public async Task<ActionResult<CommentDto>> Post([FromForm] CreateCommentForm form, CancellationToken ct)
    {
        var files = new List<AttachmentInput>();
        foreach (var file in form.Attachments ?? [])
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms, ct);
                files.Add(new AttachmentInput(file.FileName, file.ContentType, ms.ToArray()));
            }

        try
        {
            return Ok(await service.CreateAsync(
                new CreateCommentRequest(form.UserName, form.Email, form.HomePage, form.Text, form.CaptchaId,
                    form.CaptchaAnswer, form.ParentId), files, HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(), ct));
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}

public sealed class CreateCommentForm
{
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? HomePage { get; set; }
    public string Text { get; set; } = "";
    public string CaptchaId { get; set; } = "";
    public string CaptchaAnswer { get; set; } = "";
    public Guid? ParentId { get; set; }
    public List<IFormFile>? Attachments { get; set; }
}

[ApiController]
[Route("api/attachments")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class AttachmentsController(CommentsDbContext db) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var a = await db.Attachments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (a is null || !System.IO.File.Exists(a.StorageReference)) return NotFound();
        return PhysicalFile(a.StorageReference, a.ContentType, a.OriginalName, true);
    }
}
