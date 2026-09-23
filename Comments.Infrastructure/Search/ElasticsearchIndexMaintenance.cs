using Comments.Application.Abstractions;
using Comments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comments.Infrastructure.Search;

/// <summary>
///     Provides maintenance operations for the Elasticsearch comment index,
///     including rebuilding the index after crashes or data inconsistencies.
/// </summary>
/// <param name="elasticService"></param>
/// <param name="db"></param>
public sealed class ElasticsearchIndexMaintenance(
    IElasticService elasticService,
    CommentsDbContext db) : ICommentIndexMaintenance
{
    private const int IndexBatchSize = 500;

    public async Task<bool> RebuildAsync(CancellationToken ct)
    {
        if (!await NeedsRebuildAsync(ct)) return false;
        if (await elasticService.IndexExistsAsync(ct))
            await elasticService.DeleteIndexAsync(ct);

        var parentIds = await db.Comments.AsNoTracking()
            .Select(x => new { x.Id, x.ParentId })
            .ToDictionaryAsync(x => x.Id, x => x.ParentId, ct);

        Guid? lastId = null;
        while (true)
        {
            var documents = await LoadBatchAsync(lastId, parentIds, ct);
            if (documents.Count == 0) break;
            await elasticService.BulkIndexAsync(documents, ct);
            lastId = documents[^1].Id;
        }

        return true;
    }

    private async Task<bool> NeedsRebuildAsync(CancellationToken ct)
    {
        if (!await elasticService.IndexExistsAsync(ct)) return true;

        var expected = await db.Comments.AsNoTracking().CountAsync(ct);
        return await elasticService.CountAsync(ct) != expected;
    }

    private async Task<List<CommentSearchDocument>> LoadBatchAsync(
        Guid? lastId,
        IReadOnlyDictionary<Guid, Guid?> parentIds,
        CancellationToken ct)
    {
        var query = db.Comments.AsNoTracking();
        if (lastId is Guid id)
            query = query.Where(x => x.Id.CompareTo(id) > 0);

        var comments = await query
            .OrderBy(x => x.Id)
            .Take(IndexBatchSize)
            .ToListAsync(ct);

        return comments
            .Select(x => new CommentSearchDocument(x, BuildAncestorIds(x.ParentId, parentIds)))
            .ToList();
    }

    private static IReadOnlyList<Guid> BuildAncestorIds(
        Guid? parentId,
        IReadOnlyDictionary<Guid, Guid?> parentIds)
    {
        var ancestors = new List<Guid>();
        var visited = new HashSet<Guid>();
        while (parentId is Guid currentId && visited.Add(currentId))
        {
            ancestors.Add(currentId);
            if (!parentIds.TryGetValue(currentId, out parentId)) break;
        }

        ancestors.Reverse();
        return ancestors;
    }
}
