namespace Comments.Api.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var response = (HttpResponse)state;
            response.Headers.ContentSecurityPolicy =
                "default-src 'self'; script-src 'self'; style-src 'self'; " +
                "img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'self'; " +
                "frame-ancestors 'none'";
            response.Headers.XContentTypeOptions = "nosniff";
            response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            response.Headers["Permissions-Policy"] =
                "camera=(), microphone=(), geolocation=(), payment=()";
            response.Headers.XFrameOptions = "DENY";
            return Task.CompletedTask;
        }, context.Response);

        await next(context);
    }
}
