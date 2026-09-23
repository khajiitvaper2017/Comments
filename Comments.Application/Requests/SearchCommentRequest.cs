namespace Comments.Application.Requests;

public sealed record SearchCommentRequest(
    string Query,
    bool Partial = false,
    bool SearchText = true,
    bool SearchUserName = true,
    bool SearchComments = true,
    bool SearchReplies = true,
    string? Cursor = null);
