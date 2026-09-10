namespace Comments.Application.Abstractions;

public interface IRealtimeNotifier
{
    Task NotifyCommentChangedAsync(Guid commentId, CancellationToken ct);
}
