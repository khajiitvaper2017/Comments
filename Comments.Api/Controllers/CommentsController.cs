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
    /// <summary>Returns the next bounded section of root comments.</summary>
    /// <param name="sort">The sort field: createdAt, userName, or email.</param>
    /// <param name="descending">Whether to sort in descending order.</param>
    /// <param name="ct">The cancellation token for the request.</param>
    /// <param name="cursor">The continuation token returned by the previous response.</param>
    [HttpGet]
    public Task<CommentPageDto> Get([FromQuery] string sort = "createdAt",
        [FromQuery] bool descending = true, CancellationToken ct = default,
        [FromQuery] string? cursor = null)
    {
        return service.GetRootsAsync(sort, descending, ct, cursor);
    }

    /// <summary>Returns the next bounded section of replies for a comment.</summary>
    [HttpGet("{parentId:guid}/replies")]
    public Task<IReadOnlyList<CommentDto>> GetReplies(Guid parentId, CancellationToken ct)
    {
        return service.GetRepliesAsync(parentId, ct);
    }

    /// <summary>Returns the requested search ancestors in the order supplied by the caller.</summary>
    [HttpGet("ancestors")]
    public Task<IReadOnlyList<CommentDto>> GetAncestors([FromQuery] Guid[] ids, CancellationToken ct)
    {
        return service.GetAncestorsAsync(ids, ct);
    }

    /// <summary>Creates a comment or reply, with optional attachments.</summary>
    [HttpPost]
    public async Task<ActionResult<CommentDto>> Post([FromForm] CreateCommentFormModel formModel, CancellationToken ct)
    {
        // Read uploads into application data objects before passing them across the API boundary.
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
