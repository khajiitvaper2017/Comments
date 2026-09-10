using Comments.Api.Models;
using Comments.Application.Abstractions;
using Comments.Application.Data;
using Comments.Application.DTOs;
using Comments.Application.Requests;
using Comments.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Comments.Api.Controllers;

[ApiController]
[Route("api/comments")]
public sealed class CommentsController(ICommentService service) : ControllerBase
{
    [HttpGet]
    public Task<CommentPageDto> Get([FromQuery] int page = 1, [FromQuery] string sort = "createdAt",
        [FromQuery] bool descending = true, CancellationToken ct = default)
    {
        return service.GetRootsAsync(page, sort, descending, ct);
    }

    [HttpPost]
    public async Task<ActionResult<CommentDto>> Post([FromForm] CreateCommentFormModel formModel, CancellationToken ct)
    {
        var files = new List<AttachmentInput>();
        foreach (var file in formModel.Attachments ?? [])
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            files.Add(new AttachmentInput(file.FileName, file.ContentType, ms.ToArray()));
        }

        try
        {
            return Ok(await service.CreateAsync(
                new CreateCommentRequest(formModel.UserName, formModel.Email, formModel.HomePage, formModel.Text,
                    formModel.CaptchaId,
                    formModel.CaptchaAnswer, formModel.ParentId), files,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(), ct));
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
