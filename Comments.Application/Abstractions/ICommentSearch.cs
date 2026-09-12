using Comments.Application.DTOs;

namespace Comments.Application.Abstractions;

public interface ICommentSearch
{
    Task<CommentPageDto> SearchAsync(string query, int page, CancellationToken ct);
}
