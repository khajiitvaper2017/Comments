namespace Comments.Application.DTOs;

public sealed record CommentTotalsDto(int TotalRootCount, int TotalReplyCount);

public sealed record CommentTotalsCacheResult(CommentTotalsDto? Value, long Version);
