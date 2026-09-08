namespace Comments.Domain;

public sealed class Comment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ParentId { get; set; }
    public Guid RootId { get; set; }
    public required string UserName { get; set; }
    public required string Email { get; set; }
    public string? HomePage { get; set; }
    public required string RawText { get; set; }
    public required string SanitizedText { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public Comment? Parent { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}

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