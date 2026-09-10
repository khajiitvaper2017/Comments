namespace Comments.Infrastructure.Messaging.Handlers;

public interface IRabbitMessageHandler
{
    Task HandleAsync(string type, string payload, CancellationToken ct);
}
