using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using Comments.Application.Requests;
using JetBrains.Annotations;

namespace Comments.Api.GraphQL;

/// <summary>Read-only GraphQL entry points for comments and search.</summary>
[UsedImplicitly]
public sealed class Query
{
    [UsedImplicitly]
    public Task<CommentPageDto> Comments(
        string sort = "createdAt",
        bool descending = true,
        [Service] ICommentService service = null!,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        return service.GetRootsAsync(sort, descending, cursor, cancellationToken);
    }

    [UsedImplicitly]
    public Task<IReadOnlyList<CommentDto>> Replies(
        Guid parentId,
        [Service] ICommentService service,
        CancellationToken cancellationToken = default)
    {
        return service.GetRepliesAsync(parentId, cancellationToken);
    }

    [UsedImplicitly]
    public Task<IReadOnlyList<CommentDto>> Ancestors(
        IReadOnlyList<Guid> ids,
        [Service] ICommentService service,
        CancellationToken cancellationToken = default)
    {
        return service.GetAncestorsAsync(ids, cancellationToken);
    }

    [UsedImplicitly]
    public Task<CommentPageDto> Search(
        string query,
        bool partial = false,
        bool searchText = true,
        bool searchUserName = true,
        bool searchComments = true,
        bool searchReplies = true,
        [Service] ICommentSearch search = null!,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var request = new SearchCommentRequest(query, partial, searchText, searchUserName, searchComments,
            searchReplies, cursor);

        return search.SearchAsync(request, cancellationToken);
    }
}
