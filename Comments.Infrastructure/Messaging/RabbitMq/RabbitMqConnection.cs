using Comments.Infrastructure.Options;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Comments.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options) : IDisposable
{
    private readonly Lazy<IConnection> connection = new(() =>
    {
        var value = options.Value;
        return new ConnectionFactory
        {
            HostName = value.Host,
            UserName = value.User,
            Password = value.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        }.CreateConnection();
    });

    public void Dispose()
    {
        if (connection.IsValueCreated) connection.Value.Dispose();
    }

    public IConnection Get()
    {
        return connection.Value;
    }
}
