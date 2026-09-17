using Comments.Application.DTOs;

namespace Comments.Application.Abstractions;

public interface ICommentSearch
{
    Task<CommentPageDto> SearchAsync(string query, bool partial, bool searchText,
        bool searchUserName, bool searchComments, bool searchReplies, CancellationToken ct,
        string? cursor = null);
}
