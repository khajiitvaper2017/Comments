using Comments.Application.Abstractions;
using Comments.Application.Data;
using Comments.Domain.Entities;
using Comments.Infrastructure.Exceptions;
using Comments.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Comments.Infrastructure.Services;

public sealed class AttachmentStorageService(IOptions<StorageOptions> options, IHostEnvironment env)
    : IAttachmentStorageService
{
    public async Task<Attachment> SaveAsync(AttachmentInput input, CancellationToken ct)
    {
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
        var stored = id + (image ? ".png" : ".txt");
        var path = Path.Combine(dir, stored);
        int? width = null;
        int? height = null;

        if (image)
        {
            using var source = Image.Load(new MemoryStream(input.Content));
            var ratio = Math.Min(320d / source.Width, 240d / source.Height);
            if (ratio < 1) source.Mutate(x => x.Resize((int)(source.Width * ratio), (int)(source.Height * ratio)));
            width = source.Width;
            height = source.Height;
            await source.SaveAsPngAsync(path, ct);
        }
        else
        {
            await File.WriteAllBytesAsync(path, input.Content, ct);
        }

        return new Attachment
        {
            OriginalName = Path.GetFileName(input.FileName),
            StoredName = stored,
            ContentType = image ? "image/png" : "text/plain",
            Size = input.Content.Length,
            StorageReference = path,
            Width = width,
            Height = height
        };
    }
}
