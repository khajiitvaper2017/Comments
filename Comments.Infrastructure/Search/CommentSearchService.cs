using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using Comments.Domain.Entities;
using Comments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comments.Infrastructure.Search;

public sealed class CommentSearchService(
    IElasticService elastic,
    CommentsDbContext db) : ICommentSearch
{
    private const int SearchPageSize = 25;

    public async Task<CommentPageDto> SearchAsync(string query, bool partial, bool searchText,
        bool searchUserName, bool searchComments, bool searchReplies, CancellationToken ct,
        string? cursor = null)
    {
        var searchTerm = query.Trim();
        if (searchTerm.Length == 0 || (!searchText && !searchUserName) ||
            (!searchComments && !searchReplies))
            return new CommentPageDto([], null, "search", false);

        var documents = await elastic.SearchAsync(
            searchTerm,
            partial,
            searchText,
            searchUserName,
            searchComments,
            searchReplies,
            cursor,
            ct);

        if (documents.Count == 0)
            return new CommentPageDto([], null, "search", false);

        var comments = await LoadCommentsAsync(documents.Select(x => x.Id), ct);
        var items = documents
            .Where(document => comments.TryGetValue(document.Id, out _))
            .Select(document => MapSearchHit(comments[document.Id], document))
            .ToList();

        var nextCursor = documents.Count == SearchPageSize
            ? documents[^1].Id.ToString()
            : null;

        return new CommentPageDto(items, nextCursor, "search", false);
    }

    private async Task<Dictionary<Guid, Comment>> LoadCommentsAsync(
        IEnumerable<Guid> ids,
        CancellationToken ct)
    {
        var commentIds = ids.ToHashSet();
        return await db.Comments
            .AsNoTracking()
            .Where(comment => !comment.IsDeleted && commentIds.Contains(comment.Id))
            .Include(comment => comment.Attachments)
            .ToDictionaryAsync(comment => comment.Id, ct);
    }

    private static CommentDto MapSearchHit(Comment comment, CommentSearchDocument document)
    {
        return new CommentDto(comment.Id, comment.ParentId, comment.UserName, comment.Email, comment.HomePage,
            comment.Text, comment.CreatedAtUtc,
            comment.Attachments.Select(a => new AttachmentDto(a.Id, a.OriginalName, a.ContentType, a.Size,
                a.Width, a.Height)).ToList(), [], comment.ReplyCount, true)
        {
            AncestorIds = document.AncestorIds
        };
    }
}
