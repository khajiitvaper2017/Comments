using Comments.Application.Abstractions;
using Comments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comments.Infrastructure.Search;

public sealed class CommentIndexer(
    IElasticService elastic,
    CommentsDbContext db) : ICommentIndexer
{
    public async Task IndexAsync(Guid commentId, CancellationToken ct)
    {
        var comment = await db.Comments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == commentId, ct);
        if (comment is null || comment.IsDeleted) return;

        var ancestors = new List<Guid>();
        var parentId = comment.ParentId;
        var visited = new HashSet<Guid>();
        while (parentId is Guid currentId && visited.Add(currentId))
        {
            ancestors.Add(currentId);
            parentId = await db.Comments.AsNoTracking()
                .Where(x => !x.IsDeleted && x.Id == currentId)
                .Select(x => x.ParentId)
                .SingleOrDefaultAsync(ct);
        }

        ancestors.Reverse();
        await elastic.IndexAsync(new CommentSearchDocument(comment, ancestors), ct);
    }
}
