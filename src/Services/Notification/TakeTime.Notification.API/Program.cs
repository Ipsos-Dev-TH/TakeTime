using Microsoft.EntityFrameworkCore;
using Serilog;
using TakeTime.Core.Extensions;
using TakeTime.MultiTenancy;
using TakeTime.Notification.Application.Interfaces;
using TakeTime.Notification.Application.Commands;
using TakeTime.Notification.Application.Services;
using TakeTime.Notification.Infrastructure.Providers;
using TakeTime.Notification.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "Notification")
        .WriteTo.Console();
});

// Add Core services (MediatR, FluentValidation, pipeline behaviors)
builder.Services.AddCore(typeof(TakeTime.Notification.Application.Commands.SendNotificationCommand).Assembly);

// Add multi-tenancy services
builder.Services.AddMultiTenancy(builder.Configuration);

// Register notification providers
builder.Services.AddSingleton<INotificationProvider, EmailNotificationProvider>();
builder.Services.AddSingleton<INotificationProvider, LineNotificationProvider>();
builder.Services.AddSingleton<INotificationProvider, TelegramNotificationProvider>();
builder.Services.AddSingleton<INotificationProvider, SmsNotificationProvider>();

// Register notification dispatcher
builder.Services.AddScoped<NotificationDispatcher>();

// Register DbContext
builder.Services.AddDbContext<NotificationDbContext>((serviceProvider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
});

// Register HTTP client factory for LINE, Telegram, and SMS providers
builder.Services.AddHttpClient("LINE");
builder.Services.AddHttpClient("Telegram");
builder.Services.AddHttpClient("SMS");

// Register application repositories
builder.Services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
builder.Services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
builder.Services.AddScoped<IChatRepository, ChatRepository>();

// Add controllers
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TakeTime Notification API",
        Version = "v1",
        Description = "API for sending notifications via Email, SMS, LINE, and Telegram with multi-tenant support."
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add authentication
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = builder.Configuration["Identity:Authority"];
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Identity:Audience"] ?? "taketime-notification-api"
        };
    });

builder.Services.AddAuthorization();

// Add health checks
builder.Services.AddHealthChecks();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TakeTime Notification API v1");
    });
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

Log.Information("TakeTime Notification API starting up...");

app.Run();
