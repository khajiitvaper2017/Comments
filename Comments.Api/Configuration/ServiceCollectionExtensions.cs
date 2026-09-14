using System.Threading.RateLimiting;
using Comments.Api.GraphQL;
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
using Path = System.IO.Path;

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
        services.AddGraphQLServer()
            .AddQueryType<Query>()
            // Keep GraphQL's cycle protection aligned with the application's 24-level reply limit.
            .RemoveMaxAllowedFieldCycleDepthRule()
            .AddMaxAllowedFieldCycleDepthRule(24);
        return services;
    }

    public static IServiceCollection AddCommentsRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Load tests use the same flag as the CAPTCHA bypass and must measure the application,
        // not the protection layer.
        if (configuration.GetValue<bool>("Captcha:EnableLoadTestBypass"))
            return services;

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

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

    public static IServiceCollection AddApplicationServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        var loadTestCaptchaEnabled = configuration.GetValue<bool>("Captcha:EnableLoadTestBypass");
        if (loadTestCaptchaEnabled)
            services.AddSingleton<ICaptchaService, LoadTestCaptchaService>();
        else
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
