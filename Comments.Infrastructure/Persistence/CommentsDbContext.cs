using System.Linq.Expressions;
using Comments.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Comments.Infrastructure.Persistence;

public sealed class CommentsDbContext(DbContextOptions<CommentsDbContext> options) : DbContext(options)
{
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        foreach (var entityType in b.Model.GetEntityTypes())
        {
            var property = entityType.ClrType.GetProperty("IsDeleted");
            if (property?.PropertyType != typeof(bool)) continue;

            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var isDeleted = Expression.Property(parameter, property);
            b.Entity(entityType.ClrType).HasQueryFilter(
                Expression.Lambda(Expression.Not(isDeleted), parameter));
        }

        var c = b.Entity<Comment>();
        c.HasKey(x => x.Id);
        c.Property(x => x.UserName).HasMaxLength(100).IsRequired();
        c.Property(x => x.Email).HasMaxLength(254).IsRequired();
        c.Property(x => x.HomePage).HasMaxLength(2048);
        c.Property(x => x.Text).HasMaxLength(5000).IsRequired();
        c.Property(x => x.ReplyCount).IsRequired();
        c.Property(x => x.IpAddress).HasMaxLength(64);
        c.Property(x => x.UserAgent).HasMaxLength(512);
        c.HasOne(x => x.Parent).WithMany(x => x.Replies).HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
        c.HasIndex(x => new { x.CreatedAtUtc, x.Id })
            .HasDatabaseName("IX_Comments_ActiveRoots_CreatedAtUtc_Id")
            .HasFilter("[ParentId] IS NULL AND [IsDeleted] = 0");
        c.HasIndex(x => new { x.UserName, x.Id })
            .HasDatabaseName("IX_Comments_ActiveRoots_UserName_Id")
            .HasFilter("[ParentId] IS NULL AND [IsDeleted] = 0");
        c.HasIndex(x => new { x.Email, x.Id })
            .HasDatabaseName("IX_Comments_ActiveRoots_Email_Id")
            .HasFilter("[ParentId] IS NULL AND [IsDeleted] = 0");
        c.HasIndex(x => new { x.ParentId, x.CreatedAtUtc, x.Id })
            .HasDatabaseName("IX_Comments_ActiveReplies_ParentId_CreatedAtUtc_Id")
            .HasFilter("[IsDeleted] = 0");
        c.HasIndex(x => new { x.RootId, x.CreatedAtUtc, x.Id })
            .HasDatabaseName("IX_Comments_ActiveTree_RootId_CreatedAtUtc_Id")
            .HasFilter("[IsDeleted] = 0");
        var a = b.Entity<Attachment>();
        a.HasKey(x => x.Id);
        a.Property(x => x.OriginalName).HasMaxLength(255).IsRequired();
        a.Property(x => x.StoredName).HasMaxLength(255).IsRequired();
        a.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        a.Property(x => x.StorageReference).HasMaxLength(500).IsRequired();
        a.Property(x => x.ProcessingStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        a.HasOne(x => x.Comment).WithMany(x => x.Attachments).HasForeignKey(x => x.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        var o = b.Entity<OutboxMessage>();
        o.HasKey(x => x.Id);
        o.Property(x => x.Type).HasMaxLength(200).IsRequired();
        o.Property(x => x.Payload).IsRequired();
        o.Property(x => x.LastError).HasMaxLength(2000);
        o.HasIndex(x => new { x.ProcessedAtUtc, x.DeadLetteredAtUtc, x.OccurredAtUtc });
    }

    private void HandleSoftDeleted()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Deleted ||
                entry.Metadata.FindProperty("IsDeleted")?.ClrType != typeof(bool)) continue;

            entry.CurrentValues["IsDeleted"] = true;
            entry.State = EntityState.Modified;
        }
    }

    public override int SaveChanges()
    {
        HandleSoftDeleted();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        HandleSoftDeleted();
        return base.SaveChangesAsync(cancellationToken);
    }
}
