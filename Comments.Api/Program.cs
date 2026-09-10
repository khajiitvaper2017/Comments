using Comments.Application.Abstractions;
using Comments.Api.Middleware;
using Comments.Infrastructure.Exceptions;
using Comments.Infrastructure.Options;
using Comments.Infrastructure.Persistence;
using Comments.Infrastructure.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
builder.Services.AddDbContext<CommentsDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Comments")));
builder.Services.AddSingleton<ICaptchaService, CaptchaService>();
builder.Services.AddScoped<ITextValidationService, TextValidationService>();
builder.Services.AddScoped<IAttachmentStorageService, AttachmentStorageService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<SecurityHeadersMiddleware>();

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

app.UseDefaultFiles();
app.MapStaticAssets();
app.UseHttpsRedirection();
app.MapHealthChecks("/health");
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
