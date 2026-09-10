using Comments.Application.DTOs;

namespace Comments.Application.Abstractions;

public interface ICommentCache
{
    Task<CommentPageDto?> GetAsync(int page, string sort, bool descending, CancellationToken ct);
    Task SetAsync(int page, string sort, bool descending, CommentPageDto value, CancellationToken ct);
    Task InvalidateAsync(CancellationToken ct);
}
