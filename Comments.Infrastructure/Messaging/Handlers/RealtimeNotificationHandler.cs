using System.Text.Json;
using Comments.Application.Abstractions;
using Comments.Application.DTOs;
using Comments.Application.Events;
using Comments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Comments.Infrastructure.Messaging.Handlers;

public sealed class RealtimeNotificationHandler(
    IRealtimeNotifier notifier,
    CommentsDbContext db) : IRabbitMessageHandler
{
    public async Task HandleAsync(string type, string payload, CancellationToken ct)
    {
        var commentId = type switch
        {
            nameof(CommentCreated) => JsonSerializer.Deserialize<CommentCreated>(payload)!.CommentId,
            nameof(ReplyCreated) => JsonSerializer.Deserialize<ReplyCreated>(payload)!.CommentId,
            _ => throw new InvalidOperationException($"Unsupported realtime event '{type}'.")
        };

        var comment = await db.Comments
            .AsNoTracking()
            .Include(x => x.Attachments)
            .SingleOrDefaultAsync(x => x.Id == commentId && !x.IsDeleted, ct);
        if (comment is null) return;

        var dto = new CommentDto(
            comment.Id,
            comment.ParentId,
            comment.UserName,
            comment.Email,
            comment.HomePage,
            comment.Text,
            comment.CreatedAtUtc,
            comment.Attachments
                .Select(a => new AttachmentDto(a.Id, a.OriginalName, a.ContentType, a.Size, a.Width, a.Height))
                .ToList(),
            [],
            comment.ReplyCount);

        await notifier.NotifyCommentChangedAsync(dto, ct);
    }
}
