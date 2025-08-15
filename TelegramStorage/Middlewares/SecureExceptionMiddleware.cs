using System.Net;
using System.Text.Json;

namespace TelegramStorage.Middlewares;

public class SecureExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecureExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public SecureExceptionMiddleware(
        RequestDelegate next, 
        ILogger<SecureExceptionMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Generate a unique error ID for tracking
        var errorId = Guid.NewGuid().ToString("N")[..8];
        
        // Log the complete exception with error ID
        _logger.LogError(exception, 
            "Unhandled exception occurred. ErrorId: {ErrorId}, Path: {Path}, Method: {Method}, User: {User}",
            errorId, 
            context.Request.Path, 
            context.Request.Method,
            context.User?.Identity?.Name ?? "Anonymous");

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = GetStatusCode(exception);

        var response = CreateErrorResponse(exception, errorId);
        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });
        
        await context.Response.WriteAsync(jsonResponse);
    }

    private static int GetStatusCode(Exception exception)
    {
        return exception switch
        {
            ArgumentException => (int)HttpStatusCode.BadRequest,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            FileNotFoundException => (int)HttpStatusCode.NotFound,
            NotSupportedException => (int)HttpStatusCode.MethodNotAllowed,
            TimeoutException => (int)HttpStatusCode.RequestTimeout,
            InvalidOperationException => (int)HttpStatusCode.Conflict,
            _ => (int)HttpStatusCode.InternalServerError
        };
    }

    private object CreateErrorResponse(Exception exception, string errorId)
    {
        // Only show detailed error information in development
        if (_environment.IsDevelopment())
        {
            return new
            {
                ErrorId = errorId,
                Message = "An error occurred while processing your request.",
                Details = exception.Message,
                Type = exception.GetType().Name,
                StackTrace = exception.StackTrace
            };
        }

        // In production, show generic messages only
        var userFriendlyMessage = GetUserFriendlyMessage(exception);
        
        return new
        {
            ErrorId = errorId,
            Message = userFriendlyMessage,
            Details = "Please contact support with the Error ID if this problem persists."
        };
    }

    private static string GetUserFriendlyMessage(Exception exception)
    {
        return exception switch
        {
            ArgumentException => "Invalid request parameters provided.",
            UnauthorizedAccessException => "You are not authorized to access this resource.",
            FileNotFoundException => "The requested resource was not found.",
            NotSupportedException => "The requested operation is not supported.",
            TimeoutException => "The request timed out. Please try again.",
            InvalidOperationException => "The requested operation cannot be completed at this time.",
            _ => "An unexpected error occurred while processing your request."
        };
    }
}