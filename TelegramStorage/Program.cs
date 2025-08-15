using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TelegramStorage.Configuration;
using TelegramStorage.Data;
using TelegramStorage.Middlewares;
using TelegramStorage.Services;

var builder = WebApplication.CreateBuilder(args);

// Load security settings first
var securitySettings = new SecuritySettings();
builder.Configuration.GetSection("SecuritySettings").Bind(securitySettings);
builder.Services.AddSingleton(securitySettings);

// Configure secure file upload limits
builder.WebHost.ConfigureKestrel(options =>
{
    // Set reasonable limits instead of unlimited
    options.Limits.MaxRequestBodySize = securitySettings.FileUpload.MaxFileSizeBytes;
    options.Limits.MinRequestBodyDataRate = new MinDataRate(100, TimeSpan.FromSeconds(10));
    options.Limits.MinResponseDataRate = new MinDataRate(100, TimeSpan.FromSeconds(10));
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
    options.Limits.MaxConcurrentConnections = 100;
    options.Limits.MaxConcurrentUpgradedConnections = 100;
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add memory cache for rate limiting
builder.Services.AddMemoryCache();

// Configure secure file upload options
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = securitySettings.FileUpload.MaxFileSizeBytes;
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = securitySettings.FileUpload.MaxFileSizeBytes;
    options.Limits.MinRequestBodyDataRate = new MinDataRate(100, TimeSpan.FromSeconds(10));
    options.Limits.MinResponseDataRate = new MinDataRate(100, TimeSpan.FromSeconds(10));
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
});

builder.Services.Configure<FormOptions>(options =>
{
    // Secure form options with reasonable limits
    options.ValueLengthLimit = 1024 * 1024; // 1MB for form values
    options.MultipartBodyLengthLimit = securitySettings.FileUpload.MaxFileSizeBytes;
    options.MultipartHeadersLengthLimit = 16384; // 16KB for headers
    options.MultipartBoundaryLengthLimit = 128; // 128 bytes for boundary
    options.BufferBodyLengthLimit = securitySettings.FileUpload.MaxFileSizeBytes;
    options.MemoryBufferThreshold = 65536; // 64KB memory threshold
});

// Validate and log configuration status
SecretsManager.ValidateRequiredSecrets(builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>());
SecretsManager.LogConfigurationStatus(builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>(), builder.Configuration);

// Configure database with secure connection string
var connectionString = SecretsManager.GetSecret(
    "DATABASE_CONNECTION_STRING", 
    "ConnectionStrings:DefaultConnection", 
    builder.Configuration);

builder.Services.AddDbContext<TelegramStorageContext>(options =>
    options.UseNpgsql(connectionString));

var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);
builder.Services.AddSingleton(jwtSettings);

// Configure Telegram settings with secure secrets
var telegramSettings = new TelegramSettings();
builder.Configuration.GetSection("TelegramSettings").Bind(telegramSettings);

// Override with environment variables if present
var botToken = SecretsManager.GetSecret(
    "TELEGRAM_BOT_TOKEN", 
    "TelegramSettings:BotToken", 
    builder.Configuration);
telegramSettings.BotToken = botToken;

var storageChatId = SecretsManager.GetSecret(
    "TELEGRAM_STORAGE_CHAT_ID", 
    "TelegramSettings:StorageChatId", 
    builder.Configuration);
telegramSettings.StorageChatId = storageChatId;

builder.Services.AddSingleton(telegramSettings);

// Configure JWT with secure key management
builder.Services.AddScoped<IJwtService, JwtService>();

// Get secure JWT key from environment variables
var secureKey = SecretsManager.GetSecret(
    "JWT_SECRET_KEY", 
    "JwtSettings:SecretKey", 
    builder.Configuration);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secureKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };
    });

// Add security services
builder.Services.AddScoped<IInputSanitizationService, InputSanitizationService>();
builder.Services.AddScoped<IFileValidationService, FileValidationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITelegramService, TelegramService>();
builder.Services.AddScoped<IFileService, FileService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Security middleware pipeline
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<SecureExceptionMiddleware>();

// Configure request body size based on endpoint
app.Use(async (context, next) =>
{
    var maxRequestBodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
    if (maxRequestBodySizeFeature != null)
    {
        // Only allow large requests for file upload endpoints
        if (context.Request.Path.StartsWithSegments("/api/files/upload"))
        {
            maxRequestBodySizeFeature.MaxRequestBodySize = securitySettings.FileUpload.MaxFileSizeBytes;
        }
        else
        {
            maxRequestBodySizeFeature.MaxRequestBodySize = 1024 * 1024; // 1MB for other endpoints
        }
    }
    await next();
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
