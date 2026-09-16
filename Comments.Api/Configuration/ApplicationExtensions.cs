using Comments.Api.Middleware;
using Comments.Api.Realtime;
using Comments.Infrastructure.Exceptions;
using Comments.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace Comments.Api.Configuration;

public static class ApplicationExtensions
{
    extension(WebApplication app)
    {
        public WebApplication UseCommentsApi()
        {
            app.UseMiddleware<SecurityHeadersMiddleware>();

            if (!app.Environment.IsDevelopment())
                app.UseHsts();

            ApplyDatabaseMigrations(app);
            app.UseExceptionHandler(HandleExceptions);

            if (app.Environment.IsDevelopment())
                app.UseHttpsRedirection();

            if (!app.Configuration.GetValue<bool>("Captcha:EnableLoadTestBypass"))
                app.UseRateLimiter();

            app.MapHealthChecks("/health");
            app.MapControllers();
            app.MapHub<DiscussionHub>("/hubs/discussions");
            return app;
        }

        public WebApplication UseSwagger()
        {
            app.UseSwagger(options => options.RouteTemplate = "api/{documentName}.json");
            app.UseSwaggerUI(options =>
            {
                options.RoutePrefix = "api";
                options.SwaggerEndpoint("/api/v1.json", "Comments API");
            });
            return app;
        }

        public WebApplication UseGraphQL()
        {
            app.MapGraphQL();
            return app;
        }
    }


    private static void ApplyDatabaseMigrations(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommentsDbContext>();
        db.Database.Migrate();
    }

    private static void HandleExceptions(IApplicationBuilder builder)
    {
        builder.Run(async context =>
        {
            var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            context.Response.StatusCode = error is ValidationException ? 400 : 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = error is ValidationException ? error.Message : "An unexpected error occurred."
            });
        });
    }
}
