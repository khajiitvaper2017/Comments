using Comments.Application.DTOs;
using Comments.Application.Requests;

namespace Comments.Application.Abstractions;

public interface ICommentSearch
{
    Task<CommentPageDto> SearchAsync(SearchCommentRequest request, CancellationToken ct = default);
}
