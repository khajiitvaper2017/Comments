namespace Comments.Application.DTOs;

public sealed record CommentDto(
    Guid Id,
    Guid? ParentId,
    string UserName,
    string Email,
    string? HomePage,
    string Text,
    DateTime CreatedAtUtc,
    IReadOnlyList<AttachmentDto> Attachments,
    IReadOnlyList<CommentDto> Replies);