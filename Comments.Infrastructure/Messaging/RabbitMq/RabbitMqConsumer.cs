using System.Text;
using Comments.Infrastructure.Messaging.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Comments.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqConsumer(
    RabbitMqConnection connection,
    IServiceScopeFactory scopes,
    ILogger<RabbitMqConsumer> logger) : BackgroundService
{
    private readonly List<IChannel> channels = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await CreateConsumerAsync<CacheInvalidationHandler>(RabbitMqTopology.CacheQueue, stoppingToken);
        await CreateConsumerAsync<RealtimeNotificationHandler>(RabbitMqTopology.RealtimeQueue, stoppingToken);
        await CreateConsumerAsync<SearchIndexingHandler>(RabbitMqTopology.SearchQueue, stoppingToken);
        await CreateConsumerAsync<AttachmentJobHandler>(RabbitMqTopology.AttachmentQueue, stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task CreateConsumerAsync<THandler>(string queue, CancellationToken stoppingToken)
        where THandler : IRabbitMessageHandler
    {
        var channel = await (await connection.GetAsync()).CreateChannelAsync(cancellationToken: stoppingToken);
        channels.Add(channel);
        await DeclareQueueAsync(channel, queue, stoppingToken);
        var prefetch = queue == RabbitMqTopology.AttachmentQueue ? 1 : 4;
        await channel.BasicQosAsync(0, (ushort)prefetch, false, stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<THandler>();
                await handler.HandleAsync(args.RoutingKey, Encoding.UTF8.GetString(args.Body.ToArray()), stoppingToken);
                await channel.BasicAckAsync(args.DeliveryTag, false, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                await channel.BasicNackAsync(args.DeliveryTag, false, true, stoppingToken);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "RabbitMQ message failed in queue {Queue}.", queue);
                var retryCount = GetRetryCount(args.BasicProperties);
                if (retryCount < 3)
                {
                    var properties = new BasicProperties
                    {
                        Persistent = true,
                        Headers = new Dictionary<string, object?> { ["x-retry-count"] = retryCount + 1 }
                    };
                    await channel.BasicPublishAsync(
                        RabbitMqTopology.Exchange, args.RoutingKey, false, properties, args.Body, stoppingToken);
                    await channel.BasicAckAsync(args.DeliveryTag, false, stoppingToken);
                }
                else
                {
                    await channel.BasicNackAsync(args.DeliveryTag, false, false, stoppingToken);
                }
            }
        };
        await channel.BasicConsumeAsync(queue, false, consumer, stoppingToken);
    }

    private static int GetRetryCount(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null || !properties.Headers.TryGetValue("x-retry-count", out var value)) return 0;
        return value switch
        {
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var count) => count,
            int count => count,
            long count => (int)count,
            _ => 0
        };
    }

    private static async Task DeclareQueueAsync(IChannel channel, string queue, CancellationToken ct)
    {
        await RabbitMqPublisher.DeclareExchangeAsync(channel, ct);
        var deadExchange = $"{RabbitMqTopology.Exchange}.dead";
        await channel.ExchangeDeclareAsync(deadExchange, ExchangeType.Direct, true, cancellationToken: ct);
        await channel.QueueDeclareAsync($"{queue}.dead", true, false, false, cancellationToken: ct);
        await channel.QueueDeclareAsync(queue, true, false, false,
            new Dictionary<string, object?> { ["x-dead-letter-exchange"] = deadExchange }, cancellationToken: ct);
        var bindings = queue switch
        {
            RabbitMqTopology.CacheQueue => ["CommentCreated", "ReplyCreated"],
            RabbitMqTopology.RealtimeQueue => ["CommentCreated", "ReplyCreated"],
            RabbitMqTopology.SearchQueue => ["CommentCreated", "ReplyCreated"],
            RabbitMqTopology.AttachmentQueue => ["ProcessAttachment"],
            _ => Array.Empty<string>()
        };
        foreach (var type in bindings)
        {
            await channel.QueueBindAsync(queue, RabbitMqTopology.Exchange, type, cancellationToken: ct);
            await channel.QueueBindAsync($"{queue}.dead", deadExchange, type, cancellationToken: ct);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var channel in channels) channel.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
