using Comments.Domain.Entities;
using Comments.Infrastructure.Persistence;
using Comments.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Comments.Tests;

public sealed class AttachmentProcessingServiceTests : IDisposable
{
    private readonly string storageDirectory =
        Path.Combine(Path.GetTempPath(), "comments-attachment-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(storageDirectory))
            Directory.Delete(storageDirectory, true);
    }

    [Theory]
    [InlineData("progressive_1920x1080_319kb.jpg", 320, 180)]
    [InlineData("landscape_hires_4000x2667_6.83mb.jpg", 320, 213)]
    [InlineData("web_optimized_1200x800_97kb.jpg", 320, 213)]
    [InlineData("thumbnail_150x150_10.5kb.jpg", 150, 150)]
    public async Task ProcessAsync_ConvertsJpegFixtureToBoundedWebP(string fileName, int expectedWidth,
        int expectedHeight)
    {
        Directory.CreateDirectory(storageDirectory);
        var sourcePath = Path.Combine(storageDirectory, fileName);
        File.Copy(FixturePath(fileName), sourcePath);

        await using var database = CreateDatabase();
        var attachment = await AddAttachmentAsync(database, sourcePath, fileName);
        var service = new AttachmentProcessingService(database, NullLogger<AttachmentProcessingService>.Instance);

        await service.ProcessAsync(attachment.Id, CancellationToken.None);

        var processed = await database.Attachments.SingleAsync(x => x.Id == attachment.Id);
        var processedPath = Path.Combine(storageDirectory, attachment.Id + ".webp");
        Assert.Equal(AttachmentProcessingStatus.Processed, processed.ProcessingStatus);
        Assert.Equal(attachment.Id + ".webp", processed.StoredName);
        Assert.Equal("image/webp", processed.ContentType);
        Assert.Equal(processedPath, processed.StorageReference);
        Assert.Equal(expectedWidth, processed.Width);
        Assert.Equal(expectedHeight, processed.Height);
        Assert.False(File.Exists(sourcePath));
        Assert.True(File.Exists(processedPath));

        var header = await File.ReadAllBytesAsync(processedPath);
        Assert.True(header.AsSpan(0, 4).SequenceEqual("RIFF"u8));
        Assert.True(header.AsSpan(8, 4).SequenceEqual("WEBP"u8));
    }

    [Fact]
    public async Task ProcessAsync_MarksUndecodableFixtureAsFailed()
    {
        const string fileName = "logs.txt";
        Directory.CreateDirectory(storageDirectory);
        var sourcePath = Path.Combine(storageDirectory, fileName);
        File.Copy(FixturePath(fileName), sourcePath);

        await using var database = CreateDatabase();
        var attachment = await AddAttachmentAsync(database, sourcePath, fileName);
        var service = new AttachmentProcessingService(database, NullLogger<AttachmentProcessingService>.Instance);

        await Assert.ThrowsAnyAsync<Exception>(() => service.ProcessAsync(attachment.Id, CancellationToken.None));

        var failed = await database.Attachments.SingleAsync(x => x.Id == attachment.Id);
        Assert.Equal(AttachmentProcessingStatus.Failed, failed.ProcessingStatus);
        Assert.Equal(sourcePath, failed.StorageReference);
        Assert.True(File.Exists(sourcePath));
    }

    private static string FixturePath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "TestAttachments", fileName);
    }

    private static async Task<Attachment> AddAttachmentAsync(CommentsDbContext database, string sourcePath,
        string originalName)
    {
        var comment = new Comment
        {
            UserName = "User123",
            Email = "user@example.com",
            Text = "A comment with an attachment."
        };
        var attachment = new Attachment
        {
            Comment = comment,
            OriginalName = originalName,
            StoredName = Path.GetFileName(sourcePath),
            ContentType = "image/jpeg",
            Size = new FileInfo(sourcePath).Length,
            StorageReference = sourcePath
        };
        database.Attachments.Add(attachment);
        await database.SaveChangesAsync();
        return attachment;
    }

    private static CommentsDbContext CreateDatabase()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var database = new CommentsDbContext(new DbContextOptionsBuilder<CommentsDbContext>()
            .UseSqlite(connection)
            .Options);
        database.Database.EnsureCreated();
        return database;
    }
}
