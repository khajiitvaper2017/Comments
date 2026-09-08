using Comments.Domain;

namespace Comments.Infrastructure;

public sealed class CommentsDbContext(DbContextOptions<CommentsDbContext> options) : DbContext(options)
{
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        var c = b.Entity<Comment>();
        c.HasKey(x => x.Id);
        c.Property(x => x.UserName).HasMaxLength(100).IsRequired();
        c.Property(x => x.Email).HasMaxLength(254).IsRequired();
        c.Property(x => x.HomePage).HasMaxLength(2048);
        c.Property(x => x.RawText).HasMaxLength(5000).IsRequired();
        c.Property(x => x.SanitizedText).HasMaxLength(5000).IsRequired();
        c.Property(x => x.IpAddress).HasMaxLength(64);
        c.Property(x => x.UserAgent).HasMaxLength(512);
        c.HasOne(x => x.Parent).WithMany(x => x.Replies).HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
        c.HasIndex(x => new { x.RootId, x.CreatedAtUtc });
        c.HasIndex(x => x.ParentId);
        c.HasIndex(x => x.UserName);
        c.HasIndex(x => x.Email);
        var a = b.Entity<Attachment>();
        a.HasKey(x => x.Id);
        a.Property(x => x.OriginalName).HasMaxLength(255).IsRequired();
        a.Property(x => x.StoredName).HasMaxLength(255).IsRequired();
        a.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        a.Property(x => x.StorageReference).HasMaxLength(500).IsRequired();
        a.HasOne(x => x.Comment).WithMany(x => x.Attachments).HasForeignKey(x => x.CommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}