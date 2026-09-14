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
    CommentsDbContext db) : ICommentSearch, ICommentIndexer, ICommentIndexMaintenance
{
    private const int MaxReplyDepth = 24;
    private const int PageSize = 25;
    private const int IndexBatchSize = 500;

    /// <summary>
    ///     Rebuilds the Elasticsearch index from the database if it is missing or out of sync. Shouldn't be called in normal
    ///     operation, but is useful for recovery and testing.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<bool> RebuildAsync(CancellationToken ct)
    {
        if (!await NeedsRebuildAsync(ct)) return false;
        await ResetIndexAsync(ct);

        Guid? lastId = null;
        while (true)
        {
            var documents = await LoadBatchAsync(lastId, ct);
            if (documents.Count == 0) break;
            await IndexBatchAsync(documents, ct);
            lastId = documents[^1].Id;
        }

        return true;
    }

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

    public async Task<CommentPageDto> SearchAsync(string query, int page, bool partial, bool searchText,
        bool searchUserName, CancellationToken ct)
    {
        page = Math.Max(1, page);
        query = query.Trim();
        if (query.Length == 0 || (!searchText && !searchUserName))
            return new CommentPageDto([], page, PageSize, 0, "search", false);

        var response = await client.SearchAsync<CommentSearchDocument>(request => request
            .Indices(options.Value.Index)
            .From((page - 1) * PageSize)
            .Size(PageSize)
            .TrackTotalHits(true)
            .Query(queryDefinition =>
            {
                if (partial && searchUserName)
                {
                    queryDefinition.Bool(boolQuery => boolQuery.Should(
                        innerQuery => innerQuery.MultiMatch(multiMatch =>
                        {
                            ConfigureTextSearch(multiMatch, query, searchText, searchUserName);
                        }),
                        innerQuery => innerQuery.Wildcard(wildcard => wildcard
                            .Field("userName.keyword")
                            .Value($"*{EscapeWildcard(query)}*")
                            .CaseInsensitive())));
                    return;
                }

                queryDefinition.MultiMatch(multiMatch =>
                {
                    ConfigureTextSearch(multiMatch, query, searchText, searchUserName, partial);
                });
            }), ct);

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
        var totalReplyCount = await db.CommentStatistics
            .Where(x => x.Id == 1)
            .Select(x => x.TotalReplyCount)
            .SingleAsync(ct);
        return new CommentPageDto(items, page, PageSize,
            (int)Math.Min(response.Total, int.MaxValue), "search", false, totalReplyCount);
    }

    private async Task<bool> NeedsRebuildAsync(CancellationToken ct)
    {
        var index = await client.Indices.ExistsAsync(options.Value.Index, ct);
        if (!index.Exists) return true;

        var expected = await db.Comments.AsNoTracking().CountAsync(x => !x.IsDeleted, ct);
        var actual = await client.SearchAsync<CommentSearchDocument>(request => request
            .Indices(options.Value.Index)
            .Size(0)
            .TrackTotalHits(true), ct);
        return !actual.IsValidResponse || actual.Total != expected;
    }

    private async Task ResetIndexAsync(CancellationToken ct)
    {
        var index = await client.Indices.ExistsAsync(options.Value.Index, ct);
        if (!index.Exists) return;

        var response = await client.Indices.DeleteAsync(options.Value.Index, ct);
        if (!response.IsValidResponse)
            throw new InvalidOperationException("Elasticsearch index reset failed.");
    }

    private async Task<List<CommentSearchDocument>> LoadBatchAsync(Guid? lastId, CancellationToken ct)
    {
        var query = db.Comments.AsNoTracking().Where(x => !x.IsDeleted);
        if (lastId is Guid id)
            query = query.Where(x => x.Id.CompareTo(id) > 0);

        return await query
            .OrderBy(x => x.Id)
            .Take(IndexBatchSize)
            .Select(x => new CommentSearchDocument
            {
                Id = x.Id,
                ParentId = x.ParentId,
                RootId = x.RootId,
                UserName = x.UserName,
                Text = x.SanitizedText,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(ct);
    }

    private async Task IndexBatchAsync(IReadOnlyCollection<CommentSearchDocument> documents, CancellationToken ct)
    {
        var response = await client.IndexManyAsync(documents, options.Value.Index, ct);
        if (!response.IsValidResponse || response.Errors)
            throw new InvalidOperationException("Elasticsearch bulk indexing failed.");
    }

    private static void ConfigureTextSearch(
        MultiMatchQueryDescriptor<CommentSearchDocument> descriptor,
        string query,
        bool searchText,
        bool searchUserName,
        bool partial = true)
    {
        descriptor
            .Query(query)
            .Type(partial ? TextQueryType.PhrasePrefix : TextQueryType.Phrase);
        if (searchText && searchUserName)
            descriptor.Fields(document => document.Text, document => document.UserName);
        else if (searchText)
            descriptor.Fields(document => document.Text);
        else if (searchUserName)
            descriptor.Fields(document => document.UserName);
    }

    private static string EscapeWildcard(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("?", "\\?", StringComparison.Ordinal);
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
        var replyCount = comment.DescendantCount;
        return new CommentDto(comment.Id, comment.ParentId, comment.UserName, comment.Email, comment.HomePage,
            comment.SanitizedText, comment.CreatedAtUtc,
            comment.Attachments.Select(a => new AttachmentDto(a.Id, a.OriginalName, a.ContentType, a.Size,
                a.Width, a.Height)).ToList(), replies, replyCount,
            matchedIds.Contains(comment.Id));
    }
}
