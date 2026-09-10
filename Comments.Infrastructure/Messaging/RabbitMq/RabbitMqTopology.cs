namespace Comments.Infrastructure.Messaging.RabbitMq;

public static class RabbitMqTopology
{
    public const string Exchange = "comments.events";
    public const string CacheQueue = "comments.cache-invalidation";
    public const string RealtimeQueue = "comments.realtime";
    public const string AttachmentQueue = "attachments.process";
}
