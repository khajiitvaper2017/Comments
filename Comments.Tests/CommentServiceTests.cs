using Comments.Application.Abstractions;
using Comments.Application.Data;
using Comments.Application.DTOs;
using Comments.Application.Requests;
using Comments.Domain.Entities;
using Comments.Infrastructure.Exceptions;
using Comments.Infrastructure.Persistence;
using Comments.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Comments.Tests;

public sealed class CommentServiceTests
{
    [Fact]
    public async Task InvalidRequestDoesNotVerifyCaptcha()
    {
        await using var database = CreateDatabase();
        var captcha = new FakeCaptcha();
        var service = CreateService(database, captcha);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            ValidRequest() with { UserName = "bad name" }, [], null, null, CancellationToken.None));

        Assert.False(captcha.WasVerified);
        Assert.Empty(database.Comments);
    }

    [Fact]
    public async Task InvalidCaptchaDoesNotPersistComment()
    {
        await using var database = CreateDatabase();
        var service = CreateService(database, new FakeCaptcha { Result = false });

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            ValidRequest(), [], null, null, CancellationToken.None));

        Assert.Empty(database.Comments);
    }

    [Fact]
    public async Task MissingParentDoesNotPersistReply()
    {
        await using var database = CreateDatabase();
        var service = CreateService(database, new FakeCaptcha());

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            ValidRequest() with { ParentId = Guid.NewGuid() }, [], null, null, CancellationToken.None));

        Assert.Empty(database.Comments);
    }

    [Fact]
    public async Task ValidRequestPersistsCommentAndOutboxEvent()
    {
        await using var database = CreateDatabase();
        var service = CreateService(database, new FakeCaptcha());

        var result = await service.CreateAsync(
            ValidRequest(),
            [new AttachmentInput("note.txt", "text/plain", [1, 2, 3])],
            "127.0.0.1", "test", CancellationToken.None);

        Assert.Equal("User123", result.UserName);
        Assert.Single(database.Comments);
        Assert.Single(database.Attachments);
        Assert.Contains(database.OutboxMessages, message => message.Type == "CommentCreated");
        Assert.Contains(database.OutboxMessages, message => message.Type == "ProcessAttachment");
    }

    [Fact]
    public async Task ValidReplyPersistsReplyCreatedOutboxEvent()
    {
        await using var database = CreateDatabase();
        var parent = new Comment
        {
            RootId = Guid.NewGuid(),
            UserName = "Parent1",
            Email = "parent@example.com",
            RawText = "Parent.",
            SanitizedText = "Parent."
        };
        database.Comments.Add(parent);
        await database.SaveChangesAsync();
        var service = CreateService(database, new FakeCaptcha());

        var result = await service.CreateAsync(
            ValidRequest() with { ParentId = parent.Id }, [], null, null, CancellationToken.None);

        Assert.Equal(parent.Id, result.ParentId);
        Assert.Equal("ReplyCreated", Assert.Single(database.OutboxMessages).Type);
        Assert.Equal(1, await database.Comments
            .Where(comment => comment.Id == parent.Id)
            .Select(comment => comment.DescendantCount)
            .SingleAsync());
    }

    [Fact]
    public async Task RootReplyCountIncludesAllDescendants()
    {
        await using var database = CreateDatabase();
        var root = new Comment
        {
            UserName = "Root123",
            Email = "root@example.com",
            RawText = "Root.",
            SanitizedText = "Root.",
            DescendantCount = 2
        };
        var reply = new Comment
        {
            ParentId = root.Id,
            RootId = root.Id,
            UserName = "Reply123",
            Email = "reply@example.com",
            RawText = "Reply.",
            SanitizedText = "Reply.",
            DescendantCount = 1
        };
        var nestedReply = new Comment
        {
            ParentId = reply.Id,
            RootId = root.Id,
            UserName = "Nested123",
            Email = "nested@example.com",
            RawText = "Nested reply.",
            SanitizedText = "Nested reply."
        };
        database.Comments.AddRange(root, reply, nestedReply);
        await database.SaveChangesAsync();
        var service = CreateService(database, new FakeCaptcha());

        var result = await service.GetRootsAsync(1, "createdAt", true, CancellationToken.None);

        var loadedRoot = Assert.Single(result.Items);
        Assert.Equal(2, loadedRoot.ReplyCount);
        var loadedReply = Assert.Single(loadedRoot.Replies);
        Assert.Equal(1, loadedReply.ReplyCount);
        Assert.Single(loadedReply.Replies);
    }

    [Fact]
    public async Task LargeReplyBranchIsNotExpandedAutomatically()
    {
        await using var database = CreateDatabase();
        var root = new Comment
        {
            UserName = "Root123",
            Email = "root@example.com",
            RawText = "Root.",
            SanitizedText = "Root."
        };
        root.RootId = root.Id;
        var reply = new Comment
        {
            ParentId = root.Id,
            RootId = root.Id,
            UserName = "Reply123",
            Email = "reply@example.com",
            RawText = "Reply.",
            SanitizedText = "Reply.",
            DescendantCount = 5
        };
        var descendants = Enumerable.Range(1, 5).Select(index => new Comment
        {
            ParentId = reply.Id,
            RootId = root.Id,
            UserName = $"Nested{index}",
            Email = $"nested{index}@example.com",
            RawText = $"Nested {index}.",
            SanitizedText = $"Nested {index}."
        }).ToList();
        database.Comments.AddRange(new[] { root, reply }.Concat(descendants));
        await database.SaveChangesAsync();
        var service = CreateService(database, new FakeCaptcha());

        var result = await service.GetRepliesAsync(root.Id, CancellationToken.None);

        var loadedReply = Assert.Single(result);
        Assert.Equal(5, loadedReply.ReplyCount);
        Assert.Empty(loadedReply.Replies);
    }

    private static CommentService CreateService(CommentsDbContext database, FakeCaptcha captcha)
    {
        return new CommentService(
            database,
            new TextValidationService(),
            captcha,
            new FakeAttachmentStorage(),
            new FakeCommentCache());
    }

    private static CommentsDbContext CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<CommentsDbContext>()
            .UseSqlite(connection)
            .Options;
        var database = new CommentsDbContext(options);
        database.Database.EnsureCreated();
        return database;
    }

    private static CreateCommentRequest ValidRequest()
    {
        return new CreateCommentRequest(
            "User123", "user@example.com", null, "A valid comment.", "captcha", "answer", null);
    }

    private sealed class FakeCaptcha : ICaptchaService
    {
        public bool Result { get; init; } = true;
        public bool WasVerified { get; private set; }

        public CaptchaDto Create()
        {
            return new CaptchaDto("id", "image");
        }

        public bool Verify(string id, string answer)
        {
            WasVerified = true;
            return Result;
        }
    }

    private sealed class FakeAttachmentStorage : IAttachmentStorageService
    {
        public Task<Attachment> SaveAsync(AttachmentInput input, CancellationToken ct)
        {
            return Task.FromResult(new Attachment
            {
                OriginalName = input.FileName,
                StoredName = Guid.NewGuid().ToString("N"),
                ContentType = input.ContentType,
                Size = input.Content.Length,
                StorageReference = "test/" + input.FileName
            });
        }
    }

    private sealed class FakeCommentCache : ICommentCache
    {
        public Task<CommentPageDto?> GetAsync(int page, string sort, bool descending, CancellationToken ct)
        {
            return Task.FromResult<CommentPageDto?>(null);
        }

        public Task SetAsync(int page, string sort, bool descending, CommentPageDto value, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task InvalidateAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
