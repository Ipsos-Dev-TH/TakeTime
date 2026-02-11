using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TakeTime.MultiTenancy;

var builder = WebApplication.CreateBuilder(args);

// Controllers for admin endpoints (feature management, etc.)
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TakeTime API Gateway", Version = "v1" });
});

// Multi-tenancy services (for feature management API)
builder.Services.AddMultiTenancy(builder.Configuration);

// YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? "default";
        return RateLimitPartition.GetFixedWindowLimiter(tenantId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 1000,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 50,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too many requests",
            retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                ? retryAfter.TotalSeconds : 60
        });
    };
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Middleware pipeline
app.UseCors("AllowAll");
app.UseRateLimiter();

// Tenant header forwarding
app.Use(async (context, next) =>
{
    // Extract tenant from subdomain if not in header
    if (!context.Request.Headers.ContainsKey("X-Tenant-Id"))
    {
        var host = context.Request.Host.Host;
        var parts = host.Split('.');
        if (parts.Length >= 3)
        {
            context.Request.Headers.Append("X-Tenant-Id", parts[0]);
        }
    }
    await next();
});

// Serve Admin Portal static files (wwwroot)
app.UseDefaultFiles();
app.UseStaticFiles();

// Redirect root to admin login
app.MapGet("/", () => Results.Redirect("/login.html"));

app.MapControllers();
app.MapHealthChecks("/health");

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapReverseProxy();

app.Run();
