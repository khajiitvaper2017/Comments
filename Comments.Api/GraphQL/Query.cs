using Comments.Application.Abstractions;
using Comments.Application.DTOs;

namespace Comments.Api.GraphQL;

/// <summary>Read-only GraphQL entry points for comments and search.</summary>
public sealed class Query
{
    public Task<CommentPageDto> Comments(
        string sort = "createdAt",
        bool descending = true,
        [Service] ICommentService service = null!,
        CancellationToken cancellationToken = default,
        string? cursor = null)
    {
        return service.GetRootsAsync(sort, descending, cancellationToken, cursor);
    }

    public Task<IReadOnlyList<CommentDto>> Replies(
        Guid parentId,
        [Service] ICommentService service,
        CancellationToken cancellationToken = default)
    {
        return service.GetRepliesAsync(parentId, cancellationToken);
    }

    public Task<IReadOnlyList<CommentDto>> Ancestors(
        IReadOnlyList<Guid> ids,
        [Service] ICommentService service,
        CancellationToken cancellationToken = default)
    {
        return service.GetAncestorsAsync(ids, cancellationToken);
    }

    public Task<CommentPageDto> Search(
        string query,
        bool partial = false,
        bool searchText = true,
        bool searchUserName = true,
        [Service] ICommentSearch search = null!,
        CancellationToken cancellationToken = default,
        string? cursor = null)
    {
        return search.SearchAsync(query, partial, searchText, searchUserName, cancellationToken, cursor);
    }
}
