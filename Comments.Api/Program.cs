using Comments.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services
    .AddCommentsOptions(builder.Configuration)
    .AddDatabase(builder.Configuration)
    .AddRedis(builder.Configuration)
    .AddElasticsearch(builder.Configuration)
    .AddApplicationServices(builder.Configuration)
    .AddCommentsRateLimiting(builder.Configuration)
    .AddRabbitMq();

// Api
builder.Services
    .AddCommentsApi()
    .AddSwagger()
    .AddGraphQL();

var app = builder.Build();

app.UseCommentsApi();
app.UseSwagger();
app.UseGraphQL();

app.Run();
