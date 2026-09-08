using Comments.Application.Data;
using Comments.Application.DTOs;
using Comments.Application.Requests;

namespace Comments.Application.Abstractions;

public interface ICommentService
{
    Task<CommentPageDto> GetRootsAsync(int page, string sort, bool descending, CancellationToken cancellationToken);

    Task<CommentDto> CreateAsync(CreateCommentRequest request, IReadOnlyList<AttachmentInput> attachments, string? ip,
        string? userAgent, CancellationToken cancellationToken);
}
