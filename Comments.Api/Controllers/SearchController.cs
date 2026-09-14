using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Comments.Api.Controllers;

[ApiController]
[Route("api/search")]
public sealed class SearchController(ICommentSearch search) : ControllerBase
{
    /// <summary>Searches comment text and user names through Elasticsearch.</summary>
    [HttpGet]
    public Task<CommentPageDto> Get([FromQuery] string q, [FromQuery] int page = 1,
        [FromQuery] bool partial = false,
        [FromQuery] bool searchText = true,
        [FromQuery] bool searchUserName = true,
        CancellationToken ct = default)
    {
        return search.SearchAsync(q, page, partial, searchText, searchUserName, ct);
    }
}
