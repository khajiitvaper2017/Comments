namespace Comments.Application.Events;

public sealed record AttachmentProcessed(Guid AttachmentId, bool Succeeded);
