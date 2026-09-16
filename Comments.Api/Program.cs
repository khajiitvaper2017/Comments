using Comments.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);


var swaggerEnabled = builder.Configuration.GetValue("Swagger:Enabled", false);

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
    .AddGraphQL();

if (swaggerEnabled)
    builder.Services.AddSwagger();

var app = builder.Build();

app.UseCommentsApi();
app.UseGraphQL();

if (swaggerEnabled)
    app.UseSwagger();

app.Run();
