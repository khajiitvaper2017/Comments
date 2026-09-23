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
    public Task<CommentPageDto> Get([FromQuery] string q,
        [FromQuery] bool partial = false,
        [FromQuery] bool searchText = true,
        [FromQuery] bool searchUserName = true,
        [FromQuery] bool searchComments = true,
        [FromQuery] bool searchReplies = true,
        [FromQuery] string? cursor = null,
        CancellationToken ct = default)
    {
        var request = new SearchCommentRequest(q, partial, searchText, searchUserName, searchComments,
            searchReplies, cursor);
        return search.SearchAsync(request, ct);
    }
}
