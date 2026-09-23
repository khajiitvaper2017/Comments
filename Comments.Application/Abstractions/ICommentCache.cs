using Comments.Application.DTOs;

namespace Comments.Application.Abstractions;

public interface ICommentCache
{
    Task<CommentPageDto?> GetAsync(string sort, bool descending, string? cursor = null, CancellationToken ct = default);

    Task SetAsync(string sort, bool descending, CommentPageDto value, string? cursor = null,
        CancellationToken ct = default);

    Task InvalidateAsync(CancellationToken ct);
}
