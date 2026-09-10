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
    private readonly List<IModel> channels = [];

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Each queue has its own scoped handler, while this class owns transport concerns only.
        CreateConsumer<CacheInvalidationHandler>(RabbitMqTopology.CacheQueue, stoppingToken);
        CreateConsumer<RealtimeNotificationHandler>(RabbitMqTopology.RealtimeQueue, stoppingToken);
        CreateConsumer<AttachmentJobHandler>(RabbitMqTopology.AttachmentQueue, stoppingToken);
        return Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void CreateConsumer<THandler>(
        string queue,
        CancellationToken stoppingToken)
        where THandler : IRabbitMessageHandler
    {
        // Manual acknowledgements let failed messages be retried or dead-lettered explicitly.
        var channel = connection.Get().CreateModel();
        channels.Add(channel);
        DeclareQueue(channel, queue);
        channel.BasicQos(0, 4, false);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, args) =>
        {
            try
            {
                using var scope = scopes.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<THandler>();
                await handler.HandleAsync(
                    args.RoutingKey,
                    Encoding.UTF8.GetString(args.Body.ToArray()),
                    stoppingToken);
                channel.BasicAck(args.DeliveryTag, false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                channel.BasicNack(args.DeliveryTag, false, true);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "RabbitMQ message failed in queue {Queue}.", queue);
                var retryCount = GetRetryCount(args.BasicProperties);
                if (retryCount < 3)
                {
                    var properties = channel.CreateBasicProperties();
                    properties.Persistent = true;
                    properties.Headers = new Dictionary<string, object>
                    {
                        ["x-retry-count"] = retryCount + 1
                    };
                    channel.BasicPublish(RabbitMqTopology.Exchange, args.RoutingKey, properties, args.Body);
                    channel.BasicAck(args.DeliveryTag, false);
                }
                else
                {
                    channel.BasicNack(args.DeliveryTag, false, false);
                }
            }
        };
        channel.BasicConsume(queue, false, consumer);
    }

    private static int GetRetryCount(IBasicProperties properties)
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

    private static void DeclareQueue(IModel channel, string queue)
    {
        // Dead-letter queues use the original event routing keys for inspection and replay.
        RabbitMqPublisher.DeclareExchange(channel);
        var deadExchange = $"{RabbitMqTopology.Exchange}.dead";
        channel.ExchangeDeclare(deadExchange, ExchangeType.Direct, true);
        channel.QueueDeclare($"{queue}.dead", true, false, false);
        channel.QueueDeclare(queue, true, false, false,
            new Dictionary<string, object> { ["x-dead-letter-exchange"] = deadExchange });
        var bindings = queue switch
        {
            RabbitMqTopology.CacheQueue => ["CommentCreated", "ReplyCreated"],
            RabbitMqTopology.RealtimeQueue => ["CommentCreated", "ReplyCreated"],
            RabbitMqTopology.AttachmentQueue => ["ProcessAttachment"],
            _ => Array.Empty<string>()
        };
        foreach (var type in bindings)
        {
            channel.QueueBind(queue, RabbitMqTopology.Exchange, type);
            channel.QueueBind($"{queue}.dead", deadExchange, type);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var channel in channels) channel.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
