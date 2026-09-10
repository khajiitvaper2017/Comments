using Comments.Infrastructure.Messaging.RabbitMq;
using Comments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Comments.Infrastructure.Messaging.Outbox;

public sealed class OutboxDispatcher(
    IServiceScopeFactory scopes,
    RabbitMqPublisher publisher,
    ILogger<OutboxDispatcher> logger) : BackgroundService
{
    /// <summary>Publishes committed outbox messages and marks them after successful delivery.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Polling keeps the implementation durable across API restarts without a second scheduler.
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CommentsDbContext>();
                var messages = await db.OutboxMessages
                    .Where(x => x.ProcessedAtUtc == null)
                    .OrderBy(x => x.OccurredAtUtc)
                    .Take(25)
                    .ToListAsync(stoppingToken);

                if (messages.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                foreach (var message in messages)
                    try
                    {
                        publisher.Publish(message.Type, message.Payload);
                        message.ProcessedAtUtc = DateTime.UtcNow;
                        message.LastError = null;
                    }
                    catch (Exception exception)
                    {
                        message.AttemptCount++;
                        message.LastError = exception.Message[..Math.Min(2000, exception.Message.Length)];
                        logger.LogError(exception, "Could not publish outbox message {MessageId}.", message.Id);
                    }

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox dispatcher iteration failed.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
    }
}
