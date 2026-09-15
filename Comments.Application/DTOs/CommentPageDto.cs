namespace Comments.Application.DTOs;

public sealed record CommentPageDto(
    IReadOnlyList<CommentDto> Items,
    string? NextCursor,
    string Sort,
    bool Descending);
