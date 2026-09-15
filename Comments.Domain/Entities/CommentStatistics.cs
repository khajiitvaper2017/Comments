namespace Comments.Domain.Entities;

/// <summary>
///     Stores global comment totals so read requests do not repeatedly scan the Comments table.
/// </summary>
public sealed class CommentStatistics
{
    public int Id { get; set; }
    public int TotalRootCount { get; set; }
    public int TotalReplyCount { get; set; }
}
