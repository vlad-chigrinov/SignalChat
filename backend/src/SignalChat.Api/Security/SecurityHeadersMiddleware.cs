namespace SignalChat.Api.Security;

/// <summary>
/// Adds hardening headers to every response. The API serves JSON and no HTML, so the CSP forbids
/// everything: nothing can be framed, loaded or executed from an API response.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    /// <summary>Swagger UI needs inline scripts and styles, so the strict CSP is skipped for its pages (Development only).</summary>
    private static readonly PathString SwaggerPath = "/swagger";

    public const string ContentTypeOptionsHeader = "X-Content-Type-Options";
    public const string ReferrerPolicyHeader = "Referrer-Policy";
    public const string ContentSecurityPolicyHeader = "Content-Security-Policy";

    public const string NoSniff = "nosniff";
    public const string NoReferrer = "no-referrer";
    public const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        var isSwaggerPage = environment.IsDevelopment() && context.Request.Path.StartsWithSegments(SwaggerPath);
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers[ContentTypeOptionsHeader] = NoSniff;
            headers[ReferrerPolicyHeader] = NoReferrer;
            if (!isSwaggerPage)
            {
                headers[ContentSecurityPolicyHeader] = ApiContentSecurityPolicy;
            }
            return Task.CompletedTask;
        });
        return next(context);
    }
}
