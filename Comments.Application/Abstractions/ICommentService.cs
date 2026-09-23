using Comments.Application.DTOs;
using Comments.Application.Requests;

namespace Comments.Application.Abstractions;

public interface ICommentService
{
    Task<CommentPageDto> GetRootsAsync(string sort, bool descending,
        CancellationToken cancellationToken, string? cursor = null);

    Task<IReadOnlyList<CommentDto>> GetRepliesAsync(Guid parentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CommentDto>> GetAncestorsAsync(IReadOnlyList<Guid> ids,
        CancellationToken cancellationToken);

    Task<CommentDto> CreateAsync(CreateCommentRequest request, CancellationToken cancellationToken);
}
