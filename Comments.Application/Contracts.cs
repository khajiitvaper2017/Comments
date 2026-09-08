namespace Comments.Application;

public sealed record CreateCommentRequest(
    string UserName,
    string Email,
    string? HomePage,
    string Text,
    string CaptchaId,
    string CaptchaAnswer,
    Guid? ParentId);

public sealed record AttachmentInput(string FileName, string ContentType, byte[] Content);

public sealed record CommentPage(
    IReadOnlyList<CommentDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    string Sort,
    bool Descending);

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

public sealed record AttachmentDto(Guid Id, string FileName, string ContentType, long Size, int? Width, int? Height);

public sealed record CaptchaDto(string Id, string ImageDataUrl);

public interface ICommentService
{
    Task<CommentPage> GetRootsAsync(int page, string sort, bool descending, CancellationToken cancellationToken);

    Task<CommentDto> CreateAsync(CreateCommentRequest request, IReadOnlyList<AttachmentInput> attachments, string? ip,
        string? userAgent, CancellationToken cancellationToken);
}

public interface ICaptchaService
{
    CaptchaDto Create();
    bool Verify(string id, string answer);
}

public interface ITextPolicy
{
    string SanitizeAndValidate(string input);
}