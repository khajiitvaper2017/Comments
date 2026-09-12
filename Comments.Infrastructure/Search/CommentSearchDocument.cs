namespace Comments.Infrastructure.Search;

internal sealed class CommentSearchDocument
{
    public Guid Id { get; init; }
    public Guid? ParentId { get; init; }
    public Guid RootId { get; init; }
    public required string UserName { get; init; }
    public required string Text { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
