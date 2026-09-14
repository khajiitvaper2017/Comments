namespace Comments.Domain.Entities;

/// <summary>
///     Represents statistics related to comments, including the total number of replies which we calculate separately to
///     avoid performance issues when counting replies for each comment.
/// </summary>
public sealed class CommentStatistics
{
    public int Id { get; set; }
    public int TotalReplyCount { get; set; }
}
