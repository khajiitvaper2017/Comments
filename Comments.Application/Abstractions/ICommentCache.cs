using Comments.Application.DTOs;

namespace Comments.Application.Abstractions;

public interface ICommentCache
{
    Task<CommentPageDto?> GetAsync(string sort, bool descending, CancellationToken ct, string? cursor = null);
    Task SetAsync(string sort, bool descending, CommentPageDto value, CancellationToken ct, string? cursor = null);
    Task InvalidateAsync(CancellationToken ct);
}
