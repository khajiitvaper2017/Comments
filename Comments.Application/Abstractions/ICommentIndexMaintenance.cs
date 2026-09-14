namespace Comments.Application.Abstractions;

public interface ICommentIndexMaintenance
{
    Task<bool> RebuildAsync(CancellationToken ct);
}
