using Comments.Application.DTOs;

namespace Comments.Application.Abstractions;

public interface IRealtimeNotifier
{
    Task NotifyCommentChangedAsync(CommentDto comment, CancellationToken ct);
}
