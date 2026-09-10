using Comments.Application.Abstractions;
using Comments.Application.Data;
using Comments.Domain.Entities;
using Comments.Infrastructure.Exceptions;
using Comments.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Comments.Infrastructure.Services;

public sealed class AttachmentStorageService(
    IOptions<StorageOptions> options,
    IHostEnvironment env) : IAttachmentStorageService
{
    /// <summary>Validates an upload and stores its source file for later processing.</summary>
    public async Task<Attachment> SaveAsync(AttachmentInput input, CancellationToken ct)
    {
        // Images keep their upload extension only temporarily; the worker converts them to WebP.
        var ext = Path.GetExtension(input.FileName).ToLowerInvariant();
        var image = new[] { ".jpg", ".jpeg", ".gif", ".png" }.Contains(ext);
        var text = ext == ".txt";
        if (!image && !text)
            throw new ValidationException("Only JPG, GIF, PNG, and TXT files are accepted.");
        if (text && input.Content.Length > options.Value.MaxTextBytes)
            throw new ValidationException("TXT files must be at most 100 KB.");

        var id = Guid.NewGuid();
        var dir = Path.Combine(env.ContentRootPath, options.Value.Root);
        Directory.CreateDirectory(dir);
        var stored = id + ext;
        var path = Path.Combine(dir, stored);
        await File.WriteAllBytesAsync(path, input.Content, ct);

        return new Attachment
        {
            Id = id,
            OriginalName = Path.GetFileName(input.FileName),
            StoredName = stored,
            ContentType = text ? "text/plain" : ContentTypeFor(ext),
            Size = input.Content.Length,
            StorageReference = path,
            ProcessingStatus = text ? AttachmentProcessingStatus.Processed : AttachmentProcessingStatus.Pending
        };
    }

    private static string ContentTypeFor(string extension)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }
}
