using Comments.Api.Realtime;
using Comments.Application.Abstractions;
using Comments.Infrastructure.Messaging.Handlers;
using Comments.Infrastructure.Messaging.Outbox;
using Comments.Infrastructure.Messaging.RabbitMq;
using Comments.Infrastructure.Options;
using Comments.Infrastructure.Persistence;
using Comments.Infrastructure.Search;
using Comments.Infrastructure.Services;
using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;

namespace Comments.Api.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommentsApi(this IServiceCollection services)
    {
        services.AddHealthChecks();
        services.AddControllers().AddJsonOptions(options =>
        {
            // The service limits reply nesting to 24 levels; leave room for the JSON envelope.
            options.JsonSerializerOptions.MaxDepth = 64;
        });
        services.AddOpenApi();
        services.AddSwaggerGen(options =>
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Comments.Api.xml")));
        return services;
    }

    public static IServiceCollection AddCommentsOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.Configure<ElasticsearchOptions>(configuration.GetSection("Elasticsearch"));
        return services;
    }

    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CommentsDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Comments")));
        return services;
    }

    public static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddStackExchangeRedisCache(options => options.Configuration = connection);
        services.AddSignalR().AddStackExchangeRedis(connection);
        services.AddScoped<ICommentCache, RedisCommentCache>();
        return services;
    }

    public static IServiceCollection AddElasticsearch(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("Elasticsearch").Get<ElasticsearchOptions>() ??
                      new ElasticsearchOptions();
        services.AddSingleton(new ElasticsearchClient(
            new ElasticsearchClientSettings(new Uri(options.Uri))));
        services.AddScoped<ICommentSearch, ElasticsearchCommentSearch>();
        services.AddScoped<ICommentIndexer, ElasticsearchCommentSearch>();
        services.AddHostedService<ElasticsearchIndexInitializer>();
        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<ICaptchaService, CaptchaService>();
        services.AddScoped<ITextValidationService, TextValidationService>();
        services.AddScoped<IAttachmentStorageService, AttachmentStorageService>();
        services.AddScoped<IAttachmentProcessor, AttachmentProcessingService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
        return services;
    }

    public static IServiceCollection AddRabbitMq(this IServiceCollection services)
    {
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<RabbitMqPublisher>();
        services.AddScoped<CacheInvalidationHandler>();
        services.AddScoped<RealtimeNotificationHandler>();
        services.AddScoped<AttachmentJobHandler>();
        services.AddScoped<SearchIndexingHandler>();
        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<RabbitMqConsumer>();
        return services;
    }
}
