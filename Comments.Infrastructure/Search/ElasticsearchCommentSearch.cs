using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using Comments.Domain.Entities;
using Comments.Infrastructure.Options;
using Comments.Infrastructure.Persistence;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Comments.Infrastructure.Search;

public sealed class ElasticsearchCommentSearch(
    ElasticsearchClient client,
    IOptions<ElasticsearchOptions> options,
    CommentsDbContext db,
    ILogger<ElasticsearchCommentSearch> logger) : ICommentSearch, ICommentIndexer, ICommentIndexMaintenance
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

        var parentIds = await db.Comments.AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new { x.Id, x.ParentId })
            .ToDictionaryAsync(x => x.Id, x => x.ParentId, ct);

        Guid? lastId = null;
        while (true)
        {
            var documents = await LoadBatchAsync(lastId, parentIds, ct);
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

        var ancestors = new List<Guid>();
        var parentId = comment.ParentId;
        for (var depth = 0; depth < 24 && parentId is Guid currentId; depth++)
        {
            ancestors.Add(currentId);
            parentId = await db.Comments.AsNoTracking()
                .Where(x => !x.IsDeleted && x.Id == currentId)
                .Select(x => x.ParentId)
                .SingleOrDefaultAsync(ct);
        }

        ancestors.Reverse();

        var document = new CommentSearchDocument
        {
            Id = comment.Id,
            ParentId = comment.ParentId,
            RootId = comment.RootId,
            AncestorIds = ancestors,
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

    public async Task<CommentPageDto> SearchAsync(string query, bool partial, bool searchText,
        bool searchUserName, CancellationToken ct, string? cursor = null)
    {
        query = query.Trim();
        if (query.Length == 0 || (!searchText && !searchUserName))
            return new CommentPageDto([], null, "search", false);

        var response = await client.SearchAsync<CommentSearchDocument>(request =>
        {
            request.Indices(options.Value.Index)
                .Size(PageSize)
                // The existing index maps the serialized Guid as text; sort on its
                // keyword subfield so search_after does not require fielddata.
                .Sort(sort => sort.Field("id.keyword", SortOrder.Asc));
            if (!string.IsNullOrWhiteSpace(cursor))
                request.SearchAfter(FieldValue.String(cursor));

            request.Query(queryDefinition =>
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
            });
        }, ct);

        if (!response.IsValidResponse)
        {
            logger.LogError(
                "Elasticsearch search failed for cursor {Cursor}, partial={Partial}, searchText={SearchText}, " +
                "searchUserName={SearchUserName}, status={StatusCode}, error={ProductError}. {DebugInformation}",
                cursor,
                partial,
                searchText,
                searchUserName,
                response.ApiCallDetails.HttpStatusCode,
                response.ApiCallDetails.ProductError,
                response.ApiCallDetails.DebugInformation);
            throw new InvalidOperationException("Elasticsearch search failed.");
        }

        var matchedIds = response.Documents.Select(x => x.Id).ToHashSet();
        var comments = await db.Comments.AsNoTracking()
            .Where(x => !x.IsDeleted && matchedIds.Contains(x.Id))
            .Include(x => x.Attachments)
            .ToListAsync(ct);
        var items = response.Documents.Join(comments, document => document.Id, comment => comment.Id,
            (document, comment) => MapSearchHit(comment, document)).ToList();
        var nextCursor = response.Documents.Count == PageSize ? response.Documents.Last().Id.ToString() : null;
        return new CommentPageDto(items, nextCursor, "search", false);
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

    private async Task<List<CommentSearchDocument>> LoadBatchAsync(
        Guid? lastId,
        IReadOnlyDictionary<Guid, Guid?> parentIds,
        CancellationToken ct)
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
                AncestorIds = BuildAncestorIds(x.ParentId, parentIds),
                UserName = x.UserName,
                Text = x.SanitizedText,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(ct);
    }

    private static IReadOnlyList<Guid> BuildAncestorIds(
        Guid? parentId,
        IReadOnlyDictionary<Guid, Guid?> parentIds)
    {
        var ancestors = new List<Guid>();
        for (var depth = 0; depth < 24 && parentId is Guid currentId; depth++)
        {
            ancestors.Add(currentId);
            if (!parentIds.TryGetValue(currentId, out parentId)) break;
        }

        ancestors.Reverse();
        return ancestors;
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

    private static CommentDto MapSearchHit(
        Comment comment,
        CommentSearchDocument document)
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
