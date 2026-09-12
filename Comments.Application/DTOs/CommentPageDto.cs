namespace Comments.Application.DTOs;

public sealed record CommentPageDto(
    IReadOnlyList<CommentDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    string Sort,
    bool Descending,
    int TotalReplyCount = 0);
