using System.Text.Json;
using Comments.Application.Abstractions;
using Comments.Application.Jobs;

namespace Comments.Infrastructure.Messaging.Handlers;

public sealed class AttachmentJobHandler(
    IAttachmentProcessor processor) : IRabbitMessageHandler
{
    /// <summary>
    ///     Processes the attachment and persists its status.
    /// </summary>
    public async Task HandleAsync(string type, string payload, CancellationToken ct)
    {
        if (type != nameof(ProcessAttachment))
            throw new InvalidOperationException($"Unsupported attachment event '{type}'.");

        var message = JsonSerializer.Deserialize<ProcessAttachment>(payload)
                      ?? throw new InvalidOperationException("Attachment job payload is invalid.");

        await processor.ProcessAsync(message.AttachmentId, ct);
    }
}
