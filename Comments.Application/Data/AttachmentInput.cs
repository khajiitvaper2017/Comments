namespace Comments.Application.Data;

public sealed record AttachmentInput(string FileName, string ContentType, byte[] Content);
