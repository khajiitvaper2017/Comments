using Comments.Infrastructure.Options;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Comments.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqConnection(IOptions<RabbitMqOptions> options) : IDisposable
{
    private readonly Lazy<Task<IConnection>> connection = new(() =>
    {
        var value = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = value.Host,
            UserName = value.User,
            Password = value.Password,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };
        return factory.CreateConnectionAsync();
    });

    public void Dispose()
    {
        if (connection.IsValueCreated)
            connection.Value.GetAwaiter().GetResult().Dispose();
    }

    public Task<IConnection> GetAsync()
    {
        return connection.Value;
    }
}
