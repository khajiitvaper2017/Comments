namespace Comments.Application.Events;

public sealed record ReplyCreated(Guid CommentId, Guid ParentId, DateTime CreatedAtUtc);
