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
    private const int PageSize = 25;

    public async Task<CommentPageDto> SearchAsync(string query, bool partial, bool searchText,
        bool searchUserName, CancellationToken ct, string? cursor = null)
    {
        query = query.Trim();
        if (query.Length == 0 || (!searchText && !searchUserName))
            return new CommentPageDto([], null, "search", false);

        var documents = await elastic.SearchAsync(query, partial, searchText, searchUserName, cursor, ct);
        var matchedIds = documents.Select(x => x.Id).ToHashSet();
        var comments = await db.Comments.AsNoTracking()
            .Where(x => !x.IsDeleted && matchedIds.Contains(x.Id))
            .Include(x => x.Attachments)
            .ToListAsync(ct);
        var items = documents.Join(comments, document => document.Id, comment => comment.Id,
            (document, comment) => MapSearchHit(comment, document)).ToList();
        var nextCursor = documents.Count == PageSize ? documents[^1].Id.ToString() : null;
        return new CommentPageDto(items, nextCursor, "search", false);
    }

    private static CommentDto MapSearchHit(Comment comment, CommentSearchDocument document)
    {
        var replyCount = comment.DescendantCount;
        return new CommentDto(comment.Id, comment.ParentId, comment.UserName, comment.Email, comment.HomePage,
            comment.SanitizedText, comment.CreatedAtUtc,
            comment.Attachments.Select(a => new AttachmentDto(a.Id, a.OriginalName, a.ContentType, a.Size,
                a.Width, a.Height)).ToList(), [], replyCount, true)
        {
            AncestorIds = document.AncestorIds
        };
    }
}
