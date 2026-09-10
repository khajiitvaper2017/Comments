using Comments.Application.Abstractions;
using Comments.Domain.Entities;
using Comments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Comments.Infrastructure.Services;

/// <summary>Converts an uploaded image to a lossy WebP file.</summary>
public sealed class AttachmentProcessingService(CommentsDbContext db) : IAttachmentProcessor
{
    public async Task ProcessAsync(Guid attachmentId, CancellationToken ct)
    {
        var attachment = await db.Attachments.SingleAsync(x => x.Id == attachmentId, ct);
        if (attachment.ProcessingStatus == AttachmentProcessingStatus.Processed) return;

        try
        {
            using var source = await Image.LoadAsync(attachment.StorageReference, ct);
            var ratio = Math.Min(320d / source.Width, 240d / source.Height);
            if (ratio < 1)
                source.Mutate(x => x.Resize((int)(source.Width * ratio), (int)(source.Height * ratio)));

            var dir = Path.GetDirectoryName(attachment.StorageReference)!;
            var processedName = attachment.Id + ".webp";
            var processedPath = Path.Combine(dir, processedName);
            await source.SaveAsWebpAsync(processedPath, new WebpEncoder
            {
                FileFormat = WebpFileFormatType.Lossy,
                Quality = 80,
                Method = WebpEncodingMethod.BestQuality
            }, ct);
            if (!string.Equals(processedPath, attachment.StorageReference, StringComparison.OrdinalIgnoreCase))
                File.Delete(attachment.StorageReference);

            attachment.StoredName = processedName;
            attachment.ContentType = "image/webp";
            attachment.StorageReference = processedPath;
            attachment.Width = source.Width;
            attachment.Height = source.Height;
            attachment.ProcessingStatus = AttachmentProcessingStatus.Processed;
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            attachment.ProcessingStatus = AttachmentProcessingStatus.Failed;
            await db.SaveChangesAsync(ct);
            throw;
        }
    }
}
