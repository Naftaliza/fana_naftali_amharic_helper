using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using AmharicHelper.Api.HealthChecks;
using AmharicHelper.Api.Middleware;
using AmharicHelper.Application;
using AmharicHelper.Infrastructure;
using AmharicHelper.Infrastructure.Persistence;
using AmharicHelper.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Real health check — the old /health was a static literal that could never fail, so Railway
// would report the app healthy while Postgres was down and every real request 500'd.
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Brotli/gzip response compression — cuts payload size (and time-to-first-byte on
// slower links) for JSON responses. Safe over HTTPS since responses aren't secret-length-
// sensitive (no compression-oracle risk like BREACH targets reflected secrets in JSON APIs).
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

// Output caching for cheap, cacheable public reads (tenant branding lookups) so repeat
// page loads for the same org don't round-trip to Postgres on every request.
builder.Services.AddOutputCache(o =>
{
    o.AddPolicy("org-branding", p => p.Cache().Expire(TimeSpan.FromMinutes(2)).SetVaryByRouteValue("slug"));
    // Varies by both {kind} (terms/privacy) and ?language — without both, every legal-doc
    // request would collide on one shared cache entry regardless of which document or language
    // was actually requested (SetVaryByRouteValue("slug") above is a no-op for a route with no
    // "slug" segment, so this policy is deliberately separate rather than reused).
    o.AddPolicy("legal-doc", p => p.Cache().Expire(TimeSpan.FromMinutes(10))
        .SetVaryByRouteValue("kind").SetVaryByQuery("language"));
});
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Amharic Helper API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// JWT auth
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret))
        };
    });
builder.Services.AddAuthorization();

// Rate limiting, partitioned by client IP. Protects the unauthenticated, paid-provider
// trial endpoints from abuse and slows credential stuffing / brute force on auth.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

    // Trial endpoints hit paid AI/TTS providers with no auth — keep them tight.
    options.AddPolicy("trial", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));

    // Referral endpoints are anonymous and cheap (DB only) — looser, but still capped to
    // deter directory scraping and lead-log spam.
    options.AddPolicy("referrals", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));

    // DocumentsController previously had no rate limiting at all — upload triggers OCR
    // (Haiku vision) automatically in the background, so an unbounded client could burn paid
    // Anthropic spend with no cap beyond this. The UsageLedger (see WalletService) is the real
    // business-rule cap on analyze/speech/chat; this is the abuse backstop underneath it,
    // partitioned by authenticated user rather than IP since every route here requires a JWT.
    options.AddPolicy("documents", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? ctx.User?.FindFirst("sub")?.Value
                      ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

// CORS for the Next.js frontend. Frontend:Origin may be a comma-separated list
// so the production domain plus Netlify preview/deploy URLs all pass CORS.
const string CorsPolicy = "frontend";
var frontendOrigins = (builder.Configuration["Frontend:Origin"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Local dev is reached from many hosts (localhost, phone over LAN, phone over a VS Code
        // devtunnel with a new random ID each time) - pattern-match instead of requiring every
        // one listed in Frontend:Origin, so switching between them needs no API restart.
        p.SetIsOriginAllowed(origin =>
            frontendOrigins.Contains(origin) ||
            (Uri.TryCreate(origin, UriKind.Absolute, out var uri) && IsLocalDevHost(uri.Host)));
    }
    else
    {
        p.WithOrigins(frontendOrigins);
    }
    p.AllowAnyHeader().AllowAnyMethod();
}));

static bool IsLocalDevHost(string host) =>
    host is "localhost" or "127.0.0.1"
    || host.StartsWith("192.168.", StringComparison.Ordinal)
    || host.StartsWith("10.", StringComparison.Ordinal)
    || System.Text.RegularExpressions.Regex.IsMatch(host, @"^172\.(1[6-9]|2\d|3[01])\.")
    || host.EndsWith(".devtunnels.ms", StringComparison.OrdinalIgnoreCase);

var app = builder.Build();

// Run database migrations on startup.
using (var scope = app.Services.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
    await migrator.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseResponseCompression();
app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

app.MapControllers();

// Keeps the existing { "status": "ok" } contract on success (see DEPLOY.md) while actually
// checking dependencies now; returns 503 with unhealthy check names on failure.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status == HealthStatus.Healthy ? "ok" : "unhealthy",
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() })
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
});

app.Run();
