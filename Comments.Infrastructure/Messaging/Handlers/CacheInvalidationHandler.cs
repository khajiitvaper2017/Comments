using Comments.Application.Abstractions;

namespace Comments.Infrastructure.Messaging.Handlers;

public sealed class CacheInvalidationHandler(ICommentCache cache) : IRabbitMessageHandler
{
    public Task HandleAsync(string type, string payload, CancellationToken ct)
    {
        _ = type;
        _ = payload;
        return cache.InvalidateAsync(ct);
    }
}
