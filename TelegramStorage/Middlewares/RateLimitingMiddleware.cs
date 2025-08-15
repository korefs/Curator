using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text.Json;
using TelegramStorage.Configuration;

namespace TelegramStorage.Middlewares;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly SecuritySettings _securitySettings;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        SecuritySettings securitySettings,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _securitySettings = securitySettings;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get client identifier (IP address or user ID if authenticated)
        var clientId = GetClientIdentifier(context);
        var endpoint = GetEndpointCategory(context.Request.Path);
        
        // Check rate limits based on endpoint type
        var rateLimitResult = await CheckRateLimitAsync(clientId, endpoint, context);
        
        if (!rateLimitResult.IsAllowed)
        {
            await HandleRateLimitExceeded(context, rateLimitResult);
            return;
        }

        // Record the request
        await RecordRequestAsync(clientId, endpoint);
        
        await _next(context);
    }

    private string GetClientIdentifier(HttpContext context)
    {
        // Use user ID if authenticated, otherwise use IP address
        var userId = context.User?.FindFirst("sub")?.Value ?? 
                    context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        // Get real IP address (consider X-Forwarded-For header)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return $"ip:{forwardedFor.Split(',')[0].Trim()}";
        }

        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{remoteIp}";
    }

    private EndpointCategory GetEndpointCategory(string path)
    {
        if (path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase))
            return EndpointCategory.Authentication;
        
        if (path.StartsWith("/api/files/upload", StringComparison.OrdinalIgnoreCase))
            return EndpointCategory.Upload;
        
        if (path.StartsWith("/api/files/", StringComparison.OrdinalIgnoreCase))
            return EndpointCategory.FileAccess;
        
        return EndpointCategory.General;
    }

    private async Task<RateLimitResult> CheckRateLimitAsync(string clientId, EndpointCategory endpoint, HttpContext context)
    {
        var (limit, windowMinutes) = GetRateLimitForEndpoint(endpoint);
        var cacheKey = $"rate_limit:{clientId}:{endpoint}:{DateTime.UtcNow:yyyy-MM-dd-HH-mm}";
        
        var currentCount = await GetCurrentRequestCountAsync(cacheKey);
        
        if (currentCount >= limit)
        {
            _logger.LogWarning("Rate limit exceeded for {ClientId} on {Endpoint}. Count: {Count}, Limit: {Limit}",
                clientId, endpoint, currentCount, limit);
            
            return new RateLimitResult
            {
                IsAllowed = false,
                Limit = limit,
                Remaining = 0,
                ResetTime = DateTime.UtcNow.AddMinutes(windowMinutes - (DateTime.UtcNow.Minute % windowMinutes))
            };
        }

        return new RateLimitResult
        {
            IsAllowed = true,
            Limit = limit,
            Remaining = limit - currentCount - 1,
            ResetTime = DateTime.UtcNow.AddMinutes(windowMinutes - (DateTime.UtcNow.Minute % windowMinutes))
        };
    }

    private (int limit, int windowMinutes) GetRateLimitForEndpoint(EndpointCategory endpoint)
    {
        return endpoint switch
        {
            EndpointCategory.Authentication => (_securitySettings.RateLimit.AuthAttemptsPerHour, 60),
            EndpointCategory.Upload => (_securitySettings.RateLimit.UploadRequestsPerMinute, 1),
            EndpointCategory.FileAccess => (_securitySettings.RateLimit.RequestsPerMinute, 1),
            EndpointCategory.General => (_securitySettings.RateLimit.RequestsPerMinute, 1),
            _ => (_securitySettings.RateLimit.RequestsPerMinute, 1)
        };
    }

    private async Task<int> GetCurrentRequestCountAsync(string cacheKey)
    {
        if (_cache.TryGetValue(cacheKey, out int currentCount))
        {
            return currentCount;
        }
        return 0;
    }

    private async Task RecordRequestAsync(string clientId, EndpointCategory endpoint)
    {
        var (_, windowMinutes) = GetRateLimitForEndpoint(endpoint);
        var cacheKey = $"rate_limit:{clientId}:{endpoint}:{DateTime.UtcNow:yyyy-MM-dd-HH-mm}";
        
        var currentCount = await GetCurrentRequestCountAsync(cacheKey);
        _cache.Set(cacheKey, currentCount + 1, TimeSpan.FromMinutes(windowMinutes));
    }

    private async Task HandleRateLimitExceeded(HttpContext context, RateLimitResult result)
    {
        context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        context.Response.ContentType = "application/json";
        
        // Add rate limit headers
        context.Response.Headers["X-RateLimit-Limit"] = result.Limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = result.Remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = ((DateTimeOffset)result.ResetTime).ToUnixTimeSeconds().ToString();
        context.Response.Headers["Retry-After"] = ((int)(result.ResetTime - DateTime.UtcNow).TotalSeconds).ToString();

        var response = new
        {
            error = "Rate limit exceeded",
            message = "Too many requests. Please try again later.",
            retryAfter = (int)(result.ResetTime - DateTime.UtcNow).TotalSeconds
        };

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
        
        await context.Response.WriteAsync(jsonResponse);
    }
}

public enum EndpointCategory
{
    General,
    Authentication,
    Upload,
    FileAccess
}

public class RateLimitResult
{
    public bool IsAllowed { get; set; }
    public int Limit { get; set; }
    public int Remaining { get; set; }
    public DateTime ResetTime { get; set; }
}