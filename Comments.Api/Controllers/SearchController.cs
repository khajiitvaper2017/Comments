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
    public Task<CommentPageDto> Get([FromQuery] string q,
        [FromQuery] bool partial = false,
        [FromQuery] bool searchText = true,
        [FromQuery] bool searchUserName = true,
        [FromQuery] bool searchComments = true,
        [FromQuery] bool searchReplies = true,
        CancellationToken ct = default,
        [FromQuery] string? cursor = null)
    {
        return search.SearchAsync(q, partial, searchText, searchUserName, searchComments,
            searchReplies, ct, cursor);
    }
}
