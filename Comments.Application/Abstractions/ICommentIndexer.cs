namespace Comments.Application.Abstractions;

public interface ICommentIndexer
{
    Task IndexAsync(Guid commentId, CancellationToken ct);
}
