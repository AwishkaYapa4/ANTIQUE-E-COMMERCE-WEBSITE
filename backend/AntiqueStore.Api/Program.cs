using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AntiqueStore.Api.Auth;
using AntiqueStore.Api.Cli;
using AntiqueStore.Api.Infrastructure;
using AntiqueStore.Api.Seo;
using AntiqueStore.Application;
using AntiqueStore.Application.Common;
using AntiqueStore.Application.Users;
using AntiqueStore.Domain.Enums;
using AntiqueStore.Infrastructure;
using AntiqueStore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;

// Container health probe (the slim runtime image has no curl): `dotnet AntiqueStore.Api.dll --healthcheck`.
if (args.Contains("--healthcheck"))
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
    var port = Environment.GetEnvironmentVariable("HEALTHCHECK_PORT") ?? "8080";
    try { return (await http.GetAsync($"http://localhost:{port}/health/live")).IsSuccessStatusCode ? 0 : 1; }
    catch (HttpRequestException) { return 1; }
    catch (TaskCanceledException) { return 1; }
}

var builder = WebApplication.CreateBuilder(args);

// The Firebase Auth Emulator accepts unsigned tokens. It must never be reachable outside local development/testing,
// otherwise anyone could forge an identity. Fail fast instead of starting insecurely.
if (!string.IsNullOrWhiteSpace(builder.Configuration["Firebase:AuthEmulatorHost"]) &&
    !(builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") || builder.Environment.IsEnvironment("E2E")))
{
    throw new InvalidOperationException(
        $"Firebase:AuthEmulatorHost is set in the '{builder.Environment.EnvironmentName}' environment. The auth emulator is for local development only.");
}

builder.Host.UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext());

// ---------- Services ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<SeoService>();

builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
    c.ProblemDetails.Extensions["traceId"] = c.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<AppExceptionHandler>();

builder.Services.AddAuthentication(AuthConstants.Scheme)
    .AddScheme<AuthenticationSchemeOptions, FirebaseAuthenticationHandler>(AuthConstants.Scheme, null);

var requireVerifiedEmail = builder.Configuration.GetValue("Auth:RequireVerifiedEmailForCheckout", true);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthConstants.AdminPolicy, p => p.RequireAuthenticatedUser().RequireRole(nameof(UserRole.Admin)))
    .AddPolicy(AuthConstants.VerifiedEmailPolicy, p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
        !requireVerifiedEmail || ctx.User.HasClaim(AuthConstants.EmailVerifiedClaim, "true")));
foreach (var permission in Permissions.All)
{
    builder.Services.AddAuthorizationBuilder().AddPolicy(permission, p => p.RequireAuthenticatedUser().RequireAssertion(ctx =>
        Enum.TryParse<UserRole>(ctx.User.FindFirstValue(ClaimTypes.Role), out var role) && Permissions.For(role).Contains(permission)));
}

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
    .WithHeaders("Authorization", "Content-Type", "Idempotency-Key")
    .SetPreflightMaxAge(TimeSpan.FromHours(1))));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            status = 429, title = "Too many requests. Please wait a moment and try again.", code = "rate_limited"
        }, ct);
    };
    static string Key(HttpContext c) => c.User.FindFirstValue(AuthConstants.UserIdClaim) ?? c.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(c =>
        RateLimitPartition.GetFixedWindowLimiter(Key(c), _ => new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("forms", c => RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 6, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("checkout", c => RateLimitPartition.GetFixedWindowLimiter(Key(c),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 40, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("uploads", c => RateLimitPartition.GetFixedWindowLimiter(Key(c),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddOutputCache(o =>
{
    o.AddPolicy("site", p => p.Expire(TimeSpan.FromSeconds(60)).Tag("site"));
    o.AddPolicy("catalog", p => p.Expire(TimeSpan.FromSeconds(60)).Tag("catalog"));
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Behind a reverse proxy (Docker/nginx/cloud LB) on a private network. Clear defaults only when explicitly trusted.
    if (builder.Configuration.GetValue("ReverseProxy:TrustAllProxies", false))
    {
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    }
});

builder.Services.AddOpenApi(o => o.AddDocumentTransformer((document, _, _) =>
{
    document.Info = new OpenApiInfo
    {
        Title = "Antique Heritage Store API", Version = "v1",
        Description = "Authenticate with a Firebase ID token: `Authorization: Bearer <token>`. Errors use RFC 7807 problem details with a `code`."
    };
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "Firebase ID token"
    };
    return Task.CompletedTask;
}));

builder.Services.AddHealthChecks();

var app = builder.Build();

// ---------- Operator CLI (db migrate / admin grant ...) ----------
if (CliCommands.IsCliInvocation(args))
    return await CliCommands.RunAsync(app.Services, args);

await DatabaseInitializer.InitializeAsync(app.Services, app.Logger, CancellationToken.None);

// ---------- Pipeline ----------
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

var csp = builder.Configuration["Security:ContentSecurityPolicy"];
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    headers["Cross-Origin-Opener-Policy"] = "same-origin-allow-popups"; // Google sign-in popup
    if (!context.Request.Path.StartsWithSegments("/api"))
    {
        headers.XFrameOptions = "DENY";
        if (!string.IsNullOrWhiteSpace(csp)) headers.ContentSecurityPolicy = csp;
    }
    await next();
});

app.UseSerilogRequestLogging();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.Context.Request.Path.StartsWithSegments("/uploads"))
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    }
});

// Optional: serve the built SPA (frontend/dist) from the API host with server-side SEO tags.
var distPath = builder.Configuration["Frontend:DistPath"];
var spaRoot = string.IsNullOrWhiteSpace(distPath) ? null : Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, distPath));
if (spaRoot is not null && Directory.Exists(spaRoot))
{
    var spaFiles = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(spaRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = spaFiles,
        OnPrepareResponse = ctx =>
        {
            if (ctx.Context.Request.Path.StartsWithSegments("/assets"))
                ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
    });
}

app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.UseOutputCache();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs");
}

app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapGet("/health/ready", async (AppDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.Json(new { status = "database unavailable" }, statusCode: 503))
    .AllowAnonymous();

app.MapGet("/sitemap.xml", async (HttpRequest request, SeoService seo, CancellationToken ct) =>
    Results.Content(await seo.RenderSitemapAsync(request, ct), "application/xml"));
app.MapGet("/robots.txt", (HttpRequest request, SeoService seo) => Results.Text(seo.RenderRobots(request), "text/plain"));

if (spaRoot is not null && File.Exists(Path.Combine(spaRoot, "index.html")))
{
    var indexPath = Path.Combine(spaRoot, "index.html");
    app.MapFallback(async (HttpContext context, SeoService seo) =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            return Results.Problem(statusCode: 404, title: "Endpoint not found.");
        var html = await File.ReadAllTextAsync(indexPath, context.RequestAborted);
        var head = await seo.RenderHeadAsync(context.Request, context.RequestAborted);
        html = SpaHtml.InjectHead(html, head);
        context.Response.Headers.CacheControl = "no-cache";
        return Results.Content(html, "text/html; charset=utf-8");
    });
}

app.Run();
return 0;

public static class SpaHtml
{
    private const string Start = "<!--seo:start-->";
    private const string End = "<!--seo:end-->";

    /// <summary>Replaces the default head block in index.html with route-specific SEO tags.</summary>
    public static string InjectHead(string html, string head)
    {
        var s = html.IndexOf(Start, StringComparison.Ordinal);
        var e = html.IndexOf(End, StringComparison.Ordinal);
        return s >= 0 && e > s ? html[..s] + head + html[(e + End.Length)..] : html.Replace("</head>", head + "</head>");
    }
}

public partial class Program;
