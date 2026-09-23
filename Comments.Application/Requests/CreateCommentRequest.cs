using Comments.Application.Data;

namespace Comments.Application.Requests;

public sealed record CreateCommentRequest(
    string UserName,
    string Email,
    string? HomePage,
    string Text,
    string CaptchaId,
    string CaptchaAnswer,
    Guid? ParentId,
    IReadOnlyList<AttachmentInput> Attachments,
    string? IpAddress,
    string? UserAgent);
