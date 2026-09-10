using System.Text.Json;
using Comments.Application.Abstractions;
using Comments.Application.Events;

namespace Comments.Infrastructure.Messaging.Handlers;

public sealed class RealtimeNotificationHandler(IRealtimeNotifier notifier) : IRabbitMessageHandler
{
    public Task HandleAsync(string type, string payload, CancellationToken ct)
    {
        var commentId = type switch
        {
            nameof(CommentCreated) => JsonSerializer.Deserialize<CommentCreated>(payload)!.CommentId,
            nameof(ReplyCreated) => JsonSerializer.Deserialize<ReplyCreated>(payload)!.CommentId,
            _ => throw new InvalidOperationException($"Unsupported realtime event '{type}'.")
        };

        return notifier.NotifyCommentChangedAsync(commentId, ct);
    }
}
