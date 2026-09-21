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
using Microsoft.EntityFrameworkCore;
using Path = System.IO.Path;

namespace Comments.Api.Configuration;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddCommentsApi()
        {
            services.AddHealthChecks();
            services.AddControllers().AddJsonOptions(options => { options.JsonSerializerOptions.MaxDepth = 64; });
            return services;
        }

        public IServiceCollection AddSwagger()
        {
            services.AddSwaggerGen(options =>
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Comments.Api.xml")));
            return services;
        }

        public IServiceCollection AddGraphQL()
        {
            services.AddGraphQLServer()
                .AddQueryType<Query>()
                .RemoveMaxAllowedFieldCycleDepthRule();
            return services;
        }

        public IServiceCollection AddCommentsRateLimiting(IConfiguration configuration)
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

        public IServiceCollection AddCommentsOptions(IConfiguration configuration)
        {
            services.Configure<StorageOptions>(configuration.GetSection("Storage"));
            services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
            services.Configure<ElasticsearchOptions>(configuration.GetSection("Elasticsearch"));
            return services;
        }

        public IServiceCollection AddDatabase(IConfiguration configuration)
        {
            services.AddDbContext<CommentsDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("Comments")));
            return services;
        }

        public IServiceCollection AddRedis(IConfiguration configuration)
        {
            var connection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
            services.AddStackExchangeRedisCache(options => options.Configuration = connection);
            services.AddSignalR().AddStackExchangeRedis(connection);
            services.AddScoped<ICommentCache, RedisCommentCache>();
            return services;
        }

        public IServiceCollection AddElasticsearch(IConfiguration configuration)
        {
            services.AddSingleton<IElasticService, ElasticService>();
            services.AddScoped<ICommentSearch, CommentSearchService>();
            services.AddScoped<ICommentIndexer, CommentIndexer>();
            services.AddScoped<ICommentIndexMaintenance, ElasticsearchIndexMaintenance>();
            services.AddHostedService<ElasticsearchIndexInitializer>();
            return services;
        }

        public IServiceCollection AddApplicationServices(IConfiguration configuration)
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
            services.AddHostedService<AttachmentMaintenanceService>();
            return services;
        }

        public IServiceCollection AddRabbitMq()
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
}
