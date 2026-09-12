using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using Comments.Domain.Entities;
using Comments.Infrastructure.Options;
using Comments.Infrastructure.Persistence;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Comments.Infrastructure.Search;

public sealed class ElasticsearchCommentSearch(
    ElasticsearchClient client,
    IOptions<ElasticsearchOptions> options,
    CommentsDbContext db) : ICommentSearch, ICommentIndexer
{
    private const int MaxReplyDepth = 24;
    private const int PageSize = 25;

    public async Task IndexAsync(Guid commentId, CancellationToken ct)
    {
        var comment = await db.Comments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == commentId, ct);
        if (comment is null || comment.IsDeleted) return;

        var document = new CommentSearchDocument
        {
            Id = comment.Id,
            ParentId = comment.ParentId,
            RootId = comment.RootId,
            UserName = comment.UserName,
            Text = comment.SanitizedText,
            CreatedAtUtc = comment.CreatedAtUtc
        };

        var response = await client.IndexAsync(document, request => request
            .Index(options.Value.Index)
            .Id(comment.Id), ct);
        if (!response.IsValidResponse)
            throw new InvalidOperationException($"Elasticsearch indexing failed for comment {commentId}.");
    }

    public async Task<CommentPageDto> SearchAsync(string query, int page, CancellationToken ct)
    {
        page = Math.Max(1, page);
        query = query.Trim();
        if (query.Length == 0)
            return new CommentPageDto([], page, PageSize, 0, "search", false);

        var response = await client.SearchAsync<CommentSearchDocument>(request => request
            .Indices(options.Value.Index)
            .From((page - 1) * PageSize)
            .Size(PageSize)
            .Query(queryDefinition => queryDefinition.MultiMatch(multiMatch => multiMatch
                .Query(query)
                .Type(TextQueryType.PhrasePrefix)
                .Fields(document => document.Text, document => document.UserName))), ct);

        if (!response.IsValidResponse)
            throw new InvalidOperationException("Elasticsearch search failed.");

        var rootIds = response.Documents.Select(x => x.RootId).Distinct().ToArray();
        var roots = await db.Comments.AsNoTracking()
            .Where(x => !x.IsDeleted && rootIds.Contains(x.Id))
            .Include(x => x.Attachments)
            .ToListAsync(ct);
        var replies = rootIds.Length == 0
            ? []
            : await db.Comments.AsNoTracking()
                .Where(x => !x.IsDeleted && x.ParentId != null && rootIds.Contains(x.RootId))
                .Include(x => x.Attachments)
                .OrderBy(x => x.CreatedAtUtc)
                .ToListAsync(ct);
        var all = roots.Concat(replies).ToList();
        var matchedIds = response.Documents.Select(x => x.Id).ToHashSet();
        var contextIds = BuildSearchContext(all, matchedIds);
        var items = rootIds.Join(roots, id => id, root => root.Id, (_, root) =>
            MapSearch(root, all, contextIds, matchedIds, 0)).ToList();
        var totalReplyCount = await db.Comments.CountAsync(x => x.ParentId != null && !x.IsDeleted, ct);
        return new CommentPageDto(items, page, PageSize,
            (int)Math.Min(response.Total, int.MaxValue), "search", false, totalReplyCount);
    }

    private static CommentDto Map(Comment comment, IEnumerable<Comment> all, int depth)
    {
        if (depth >= MaxReplyDepth)
            return new CommentDto(comment.Id, comment.ParentId, comment.UserName, comment.Email, comment.HomePage,
                comment.SanitizedText, comment.CreatedAtUtc,
                comment.Attachments.Select(a => new AttachmentDto(a.Id, a.OriginalName, a.ContentType, a.Size,
                    a.Width, a.Height)).ToList(), [], all.Count(x => x.ParentId == comment.Id),
                all.Any(x => x.ParentId == comment.Id));

        var replies = all.Where(x => x.ParentId == comment.Id).OrderBy(x => x.CreatedAtUtc)
            .Select(x => Map(x, all, depth + 1)).ToList();
        return new CommentDto(comment.Id, comment.ParentId, comment.UserName, comment.Email, comment.HomePage,
            comment.SanitizedText, comment.CreatedAtUtc,
            comment.Attachments.Select(a => new AttachmentDto(a.Id, a.OriginalName, a.ContentType, a.Size,
                a.Width, a.Height)).ToList(), replies, replies.Count);
    }

    private static HashSet<Guid> BuildSearchContext(IReadOnlyList<Comment> comments, HashSet<Guid> matchedIds)
    {
        var byId = comments.ToDictionary(x => x.Id);
        var context = new HashSet<Guid>(matchedIds);
        foreach (var matchedId in matchedIds)
        {
            var current = matchedId;
            while (byId.TryGetValue(current, out var comment) && comment.ParentId is Guid parentId)
            {
                if (!context.Add(parentId)) break;
                current = parentId;
            }
        }

        return context;
    }

    private static CommentDto MapSearch(
        Comment comment,
        IReadOnlyList<Comment> all,
        IReadOnlySet<Guid> contextIds,
        IReadOnlySet<Guid> matchedIds,
        int depth)
    {
        var children = all.Where(x => x.ParentId == comment.Id).OrderBy(x => x.CreatedAtUtc).ToList();
        // Show every direct reply of the root, but only follow branches leading to a hit.
        var selected = children.Where(x => contextIds.Contains(x.Id)).ToList();

        var replies = selected
            .Select(x => MapSearch(x, all, contextIds, matchedIds, depth + 1))
            .ToList();
        var hasMoreReplies = depth >= MaxReplyDepth || selected.Count < children.Count;
        return new CommentDto(comment.Id, comment.ParentId, comment.UserName, comment.Email, comment.HomePage,
            comment.SanitizedText, comment.CreatedAtUtc,
            comment.Attachments.Select(a => new AttachmentDto(a.Id, a.OriginalName, a.ContentType, a.Size,
                a.Width, a.Height)).ToList(), replies, children.Count, hasMoreReplies, matchedIds.Contains(comment.Id));
    }
}
