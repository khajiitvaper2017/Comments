using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using Comments.Application.Requests;
using Microsoft.AspNetCore.Mvc;

namespace Comments.Api.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController(ICommentSearch search) : ControllerBase
{
    /// <summary>Searches comment text and user names through Elasticsearch.</summary>
    [HttpGet]
    public Task<CommentPageDto> Get([FromQuery] SearchCommentRequest request,
        CancellationToken ct = default)
    {
        return search.SearchAsync(request, ct);
    }
}
