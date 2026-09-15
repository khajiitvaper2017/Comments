using Comments.Application.Abstractions;
using Comments.Application.DTOs;

namespace Comments.Api.GraphQL;

/// <summary>Read-only GraphQL entry points for comments and search.</summary>
public sealed class Query
{
    public Task<CommentPageDto> Comments(
        int page = 1,
        string sort = "createdAt",
        bool descending = true,
        [Service] ICommentService service = null!,
        CancellationToken cancellationToken = default)
    {
        return service.GetRootsAsync(page, sort, descending, cancellationToken);
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
        int page = 1,
        bool partial = false,
        bool searchText = true,
        bool searchUserName = true,
        [Service] ICommentSearch search = null!,
        CancellationToken cancellationToken = default)
    {
        return search.SearchAsync(query, page, partial, searchText, searchUserName, cancellationToken);
    }
}
