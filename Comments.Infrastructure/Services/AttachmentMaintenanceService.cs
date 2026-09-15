using System.Text.Json;
using Comments.Application.Jobs;
using Comments.Domain.Entities;
using Comments.Infrastructure.Options;
using Comments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Comments.Infrastructure.Services;

/// <summary>
///     Queues old image attachments for conversion and removes unreferenced storage files once at startup.
/// </summary>
public sealed class AttachmentMaintenanceService(
    IServiceScopeFactory scopes,
    IOptions<StorageOptions> storage,
    IHostEnvironment environment,
    ILogger<AttachmentMaintenanceService> logger) : BackgroundService
{
    private const int BatchSize = 25;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let migrations and the normal RabbitMQ consumers finish starting first.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var queued = await QueueImageConversionsAsync(stoppingToken);
            if (queued == 0) break;
            await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
        }

        await DeleteOrphanFilesAsync(stoppingToken);
    }

    private async Task<int> QueueImageConversionsAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommentsDbContext>();
        var candidates = await db.Attachments
            .AsNoTracking()
            // Failed jobs are terminal until an operator repairs the source file.
            .Where(x => x.ContentType.StartsWith("image/") &&
                        (x.ProcessingStatus == AttachmentProcessingStatus.Pending ||
                         (x.ProcessingStatus == AttachmentProcessingStatus.Processed &&
                          !x.StoredName.EndsWith(".webp"))))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (candidates.Count == 0) return 0;

        var candidateIds = candidates.Select(x => x.Id).ToHashSet();
        var queuedIds = (await db.OutboxMessages
                .AsNoTracking()
                .Where(x => x.Type == nameof(ProcessAttachment) &&
                            x.ProcessedAtUtc == null &&
                            x.DeadLetteredAtUtc == null)
                .Select(x => x.Payload)
                .ToListAsync(ct))
            .Select(TryGetAttachmentId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Where(candidateIds.Contains)
            .ToHashSet();

        foreach (var attachment in candidates.Where(x => !queuedIds.Contains(x.Id)))
            db.OutboxMessages.Add(new OutboxMessage
            {
                Type = nameof(ProcessAttachment),
                Payload = JsonSerializer.Serialize(new ProcessAttachment(attachment.Id))
            });

        var queued = candidates.Count(x => !queuedIds.Contains(x.Id));
        if (queued > 0) await db.SaveChangesAsync(ct);
        return queued;
    }

    private async Task DeleteOrphanFilesAsync(CancellationToken ct)
    {
        var root = Path.IsPathRooted(storage.Value.Root)
            ? storage.Value.Root
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, storage.Value.Root));
        if (!Directory.Exists(root)) return;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommentsDbContext>();
        var referencedNames = (await db.Attachments
                .AsNoTracking()
                .Select(x => new { x.StoredName, x.StorageReference })
                .ToListAsync(ct))
            .SelectMany(x => new[] { Path.GetFileName(x.StoredName), Path.GetFileName(x.StorageReference) })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            if (referencedNames.Contains(Path.GetFileName(file))) continue;

            try
            {
                File.Delete(file);
                logger.LogInformation("Deleted unreferenced attachment file {File}.", file);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not delete unreferenced attachment file {File}.", file);
            }
        }
    }

    private static Guid? TryGetAttachmentId(string payload)
    {
        try
        {
            return JsonSerializer.Deserialize<ProcessAttachment>(payload)?.AttachmentId;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
