using System.Text.Json;
using Comments.Application.Abstractions;
using Comments.Application.Events;
using Comments.Application.Jobs;
using Comments.Domain.Entities;
using Comments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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
        var eventPayload = JsonSerializer.Serialize(new AttachmentProcessed(message.AttachmentId, true));
        var alreadyRecorded = await db.OutboxMessages.AnyAsync(
            x => x.Type == nameof(AttachmentProcessed) && x.Payload == eventPayload, ct);
        if (!alreadyRecorded)
            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = nameof(AttachmentProcessed),
                Payload = eventPayload
            });
        await db.SaveChangesAsync(ct);
    }
}
