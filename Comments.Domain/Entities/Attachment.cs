namespace Comments.Domain.Entities;

public sealed class Attachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CommentId { get; set; }
    public required string OriginalName { get; set; }
    public required string StoredName { get; set; }
    public required string ContentType { get; set; }
    public long Size { get; set; }
    public required string StorageReference { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Comment Comment { get; set; } = null!;
}
