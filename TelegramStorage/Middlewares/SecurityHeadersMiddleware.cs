using TelegramStorage.Configuration;

namespace TelegramStorage.Middlewares;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecuritySettings _securitySettings;

    public SecurityHeadersMiddleware(RequestDelegate next, SecuritySettings securitySettings)
    {
        _next = next;
        _securitySettings = securitySettings;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers to the response
        AddSecurityHeaders(context);

        await _next(context);
    }

    private void AddSecurityHeaders(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Remove server information
        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        // HTTP Strict Transport Security (HSTS)
        if (_securitySettings.Headers.EnableHsts)
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
        }

        // X-Frame-Options to prevent clickjacking
        if (_securitySettings.Headers.EnableXFrameOptions)
        {
            headers["X-Frame-Options"] = "DENY";
        }

        // X-Content-Type-Options to prevent MIME sniffing
        if (_securitySettings.Headers.EnableXContentTypeOptions)
        {
            headers["X-Content-Type-Options"] = "nosniff";
        }

        // Referrer Policy
        if (_securitySettings.Headers.EnableReferrerPolicy)
        {
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        }

        // Content Security Policy
        if (_securitySettings.Headers.EnableCsp)
        {
            headers["Content-Security-Policy"] = _securitySettings.Headers.CspPolicy;
        }

        // X-XSS-Protection (for older browsers)
        headers["X-XSS-Protection"] = "1; mode=block";

        // Permissions Policy (formerly Feature Policy)
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        // Cross-Origin Resource Sharing (CORS) - restrictive by default
        headers["Access-Control-Allow-Origin"] = context.Request.Headers.Origin.FirstOrDefault() ?? "*";
        headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
        headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization";
        headers["Access-Control-Max-Age"] = "86400";

        // Custom security headers
        headers["X-Application-Security"] = "Enhanced";
        headers["X-Rate-Limit-Policy"] = "Enforced";
    }
}