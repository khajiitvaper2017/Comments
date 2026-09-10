namespace Comments.Application.Events;

public sealed record CommentCreated(Guid CommentId, DateTime CreatedAtUtc);
