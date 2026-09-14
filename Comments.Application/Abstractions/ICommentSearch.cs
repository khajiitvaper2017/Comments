using Comments.Application.DTOs;

namespace Comments.Application.Abstractions;

public interface ICommentSearch
{
    Task<CommentPageDto> SearchAsync(string query, int page, bool partial, bool searchText,
        bool searchUserName, CancellationToken ct);
}
