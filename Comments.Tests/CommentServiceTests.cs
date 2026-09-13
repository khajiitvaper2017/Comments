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

        var result = await service.CreateAsync(ValidRequest(), [], "127.0.0.1", "test", CancellationToken.None);

        Assert.Equal("User123", result.UserName);
        Assert.Single(database.Comments);
        var message = Assert.Single(database.OutboxMessages);
        Assert.Equal("CommentCreated", message.Type);
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
            throw new NotSupportedException();
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
