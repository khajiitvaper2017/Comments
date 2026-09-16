using System.Diagnostics.CodeAnalysis;
using Comments.Domain.Entities;

namespace Comments.Infrastructure.Search;

public sealed class CommentSearchDocument
{
    [SetsRequiredMembers]
    public CommentSearchDocument()
    {
    }

    [SetsRequiredMembers]
    public CommentSearchDocument(Comment comment, IReadOnlyList<Guid> ancestors)
    {
        Id = comment.Id;
        ParentId = comment.ParentId;
        RootId = comment.RootId;
        AncestorIds = ancestors;
        UserName = comment.UserName;
        Text = comment.Text;
        CreatedAtUtc = comment.CreatedAtUtc;
    }

    public Guid Id { get; init; }
    public Guid? ParentId { get; init; }
    public Guid RootId { get; init; }
    public IReadOnlyList<Guid> AncestorIds { get; init; } = [];
    public required string UserName { get; init; } = string.Empty;
    public required string Text { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
