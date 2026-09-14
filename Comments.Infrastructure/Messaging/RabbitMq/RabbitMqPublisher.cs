using System.Text;
using RabbitMQ.Client;

namespace Comments.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqPublisher(RabbitMqConnection connection) : IDisposable
{
    private readonly object sync = new();
    private IModel? channel;

    public void Dispose()
    {
        lock (sync)
        {
            channel?.Dispose();
            channel = null;
        }
    }

    public void Publish(string type, string payload)
    {
        lock (sync)
        {
            var publisherChannel = GetChannel();
            try
            {
                var properties = publisherChannel.CreateBasicProperties();
                properties.Persistent = true;
                publisherChannel.BasicPublish(
                    RabbitMqTopology.Exchange,
                    type,
                    properties,
                    Encoding.UTF8.GetBytes(payload));
                publisherChannel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
            }
            catch
            {
                publisherChannel.Dispose();
                channel = null;
                throw;
            }
        }
    }

    private IModel GetChannel()
    {
        if (channel is { IsOpen: true }) return channel;

        // Reusing the channel avoids a connection-level setup cost for every outbox event.
        channel?.Dispose();
        channel = connection.Get().CreateModel();
        DeclareExchange(channel);
        channel.ConfirmSelect();
        return channel;
    }

    public static void DeclareExchange(IModel channel)
    {
        channel.ExchangeDeclare(RabbitMqTopology.Exchange, ExchangeType.Direct, true);
    }
}
