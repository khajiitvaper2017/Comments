using System.Diagnostics.CodeAnalysis;
using Comments.Domain.Entities;

namespace Comments.Infrastructure.Search;

[method: SetsRequiredMembers]
public sealed class CommentSearchDocument(Comment comment, IReadOnlyList<Guid> ancestors)
{
    public Guid Id { get; init; } = comment.Id;
    public Guid? ParentId { get; init; } = comment.ParentId;
    public Guid RootId { get; init; } = comment.RootId;
    public IReadOnlyList<Guid> AncestorIds { get; init; } = ancestors;
    public required string UserName { get; init; } = comment.UserName;
    public required string Text { get; init; } = comment.Text;
    public DateTime CreatedAtUtc { get; init; } = comment.CreatedAtUtc;
}
