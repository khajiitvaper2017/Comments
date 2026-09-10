using System.Text.Json;
using Comments.Application.Abstractions;
using Comments.Application.Events;
using Comments.Application.Jobs;
using Comments.Domain.Entities;
using Comments.Infrastructure.Persistence;

namespace Comments.Infrastructure.Messaging.Handlers;

public sealed class AttachmentJobHandler(
    IAttachmentProcessor processor,
    CommentsDbContext db) : IRabbitMessageHandler
{
    public async Task HandleAsync(string type, string payload, CancellationToken ct)
    {
        if (type != nameof(ProcessAttachment))
            throw new InvalidOperationException($"Unsupported attachment event '{type}'.");

        var message = JsonSerializer.Deserialize<ProcessAttachment>(payload)
                      ?? throw new InvalidOperationException("Attachment job payload is invalid.");

        await processor.ProcessAsync(message.AttachmentId, ct);
        db.OutboxMessages.Add(new OutboxMessage
        {
            Type = nameof(AttachmentProcessed),
            Payload = JsonSerializer.Serialize(new AttachmentProcessed(message.AttachmentId, true))
        });
        await db.SaveChangesAsync(ct);
    }
}
