using Comments.Application.Abstractions;
using Comments.Domain.Entities;
using Comments.Infrastructure.Persistence;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Comments.Infrastructure.Services;

/// <summary>Converts an uploaded image to a lossy WebP file.</summary>
public sealed class AttachmentProcessingService(
    CommentsDbContext db,
    ILogger<AttachmentProcessingService> logger) : IAttachmentProcessor
{
    public async Task ProcessAsync(Guid attachmentId, CancellationToken ct)
    {
        var attachment = await db.Attachments.SingleOrDefaultAsync(x => x.Id == attachmentId, ct);
        if (attachment is null)
        {
            logger.LogWarning("Skipping attachment job {AttachmentId}: attachment row was not found.", attachmentId);
            return;
        }

        if (attachment.ProcessingStatus == AttachmentProcessingStatus.Processed) return;

        if (!File.Exists(attachment.StorageReference))
        {
            attachment.ProcessingStatus = AttachmentProcessingStatus.Failed;
            await db.SaveChangesAsync(ct);
            logger.LogWarning(
                "Skipping attachment job {AttachmentId}: source file {StorageReference} was not found.",
                attachmentId,
                attachment.StorageReference);
            return;
        }

        try
        {
            using var images = new MagickImageCollection();
            images.Read(attachment.StorageReference);
            if (images.Count == 0)
                throw new InvalidOperationException("The uploaded image could not be decoded.");

            // Coalescing makes each animation frame complete before it is resized and encoded.
            images.Coalesce();
            var first = images[0];
            var ratio = Math.Min(320d / first.Width, 240d / first.Height);
            var width = Math.Max(1, (int)(first.Width * Math.Min(ratio, 1)));
            var height = Math.Max(1, (int)(first.Height * Math.Min(ratio, 1)));
            foreach (var image in images)
            {
                // The dimensions above already preserve the source aspect ratio. Without this,
                // ImageMagick fits the image again and may remove a pixel from one edge.
                image.Resize(new MagickGeometry((uint)width, (uint)height)
                {
                    IgnoreAspectRatio = true
                });
                image.Quality = 80;
            }

            var dir = Path.GetDirectoryName(attachment.StorageReference)!;
            var processedName = attachment.Id + ".webp";
            var processedPath = Path.Combine(dir, processedName);
            images.Write(processedPath, MagickFormat.WebP);
            if (!string.Equals(processedPath, attachment.StorageReference, StringComparison.OrdinalIgnoreCase))
                File.Delete(attachment.StorageReference);

            attachment.StoredName = processedName;
            attachment.ContentType = "image/webp";
            attachment.StorageReference = processedPath;
            attachment.Width = (int)first.Width;
            attachment.Height = (int)first.Height;
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
