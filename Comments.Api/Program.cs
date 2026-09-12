using Comments.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddCommentsOptions(builder.Configuration)
    .AddDatabase(builder.Configuration)
    .AddRedis(builder.Configuration)
    .AddElasticsearch(builder.Configuration)
    .AddApplicationServices()
    .AddRabbitMq();

builder.Services.AddCommentsApi();

var app = builder.Build();

app.UseCommentsApi();

app.Run();
