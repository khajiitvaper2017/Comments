using System.Text;
using RabbitMQ.Client;

namespace Comments.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqPublisher(RabbitMqConnection connection) : IDisposable
{
    private readonly SemaphoreSlim sync = new(1, 1);
    private IChannel? channel;

    public void Dispose()
    {
        channel?.Dispose();
        sync.Dispose();
    }

    public async Task PublishAsync(string type, string payload, CancellationToken ct = default)
    {
        await sync.WaitAsync(ct);
        try
        {
            var publisherChannel = await GetChannelAsync(ct);
            try
            {
                await publisherChannel.BasicPublishAsync(
                    RabbitMqTopology.Exchange,
                    type,
                    false,
                    new BasicProperties { Persistent = true },
                    Encoding.UTF8.GetBytes(payload),
                    ct);
            }
            catch
            {
                publisherChannel.Dispose();
                channel = null;
                throw;
            }
        }
        finally
        {
            sync.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (channel is { IsOpen: true }) return channel;

        channel?.Dispose();
        channel = await (await connection.GetAsync()).CreateChannelAsync(cancellationToken: ct);
        await DeclareExchangeAsync(channel, ct);
        return channel;
    }

    public static Task DeclareExchangeAsync(IChannel channel, CancellationToken ct = default)
    {
        return channel.ExchangeDeclareAsync(RabbitMqTopology.Exchange, ExchangeType.Direct, true,
            cancellationToken: ct);
    }
}
