using Comments.Api.Middleware;
using Comments.Api.Realtime;
using Comments.Application.Abstractions;
using Comments.Infrastructure.Exceptions;
using Comments.Infrastructure.Messaging.Handlers;
using Comments.Infrastructure.Messaging.Outbox;
using Comments.Infrastructure.Messaging.RabbitMq;
using Comments.Infrastructure.Options;
using Comments.Infrastructure.Persistence;
using Comments.Infrastructure.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddDbContext<CommentsDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Comments")));

var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = redisConnection);

builder.Services.AddSignalR().AddStackExchangeRedis(redisConnection);
builder.Services.AddSingleton<ICaptchaService, CaptchaService>();
builder.Services.AddScoped<ITextValidationService, TextValidationService>();
builder.Services.AddScoped<IAttachmentStorageService, AttachmentStorageService>();
builder.Services.AddScoped<IAttachmentProcessor, AttachmentProcessingService>();
builder.Services.AddScoped<ICommentCache, RedisCommentCache>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddSingleton<RabbitMqConnection>();
builder.Services.AddSingleton<RabbitMqPublisher>();
builder.Services.AddScoped<CacheInvalidationHandler>();
builder.Services.AddScoped<RealtimeNotificationHandler>();
builder.Services.AddScoped<AttachmentJobHandler>();

builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHostedService<RabbitMqConsumer>();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Comments.Api.xml")));

var app = builder.Build();

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSwagger(options => options.RouteTemplate = "api/{documentName}.json");
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "api";
    options.SwaggerEndpoint("/api/v1.json", "Comments API");
});

if (!app.Environment.IsDevelopment())
    app.UseHsts();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<CommentsDbContext>().Database.Migrate();
}

app.UseExceptionHandler(e => e.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    context.Response.StatusCode = error is ValidationException ? 400 : 500;
    context.Response.ContentType = "application/json";
    await context.Response.WriteAsJsonAsync(new
        { error = error is ValidationException ? error.Message : "An unexpected error occurred." });
}));

if (app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<DiscussionHub>("/hubs/discussions");

app.Run();
