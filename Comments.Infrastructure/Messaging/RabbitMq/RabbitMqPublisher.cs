using System.Text;
using RabbitMQ.Client;

namespace Comments.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqPublisher(RabbitMqConnection connection)
{
    public void Publish(string type, string payload)
    {
        using var channel = connection.Get().CreateModel();
        DeclareExchange(channel);
        channel.ConfirmSelect();
        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        channel.BasicPublish(RabbitMqTopology.Exchange, type, properties, Encoding.UTF8.GetBytes(payload));
        channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
    }

    public static void DeclareExchange(IModel channel)
    {
        channel.ExchangeDeclare(RabbitMqTopology.Exchange, ExchangeType.Direct, true);
    }
}
