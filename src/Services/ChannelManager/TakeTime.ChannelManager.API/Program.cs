using Microsoft.EntityFrameworkCore;
using Serilog;
using TakeTime.ChannelManager.Infrastructure.Repositories;
using TakeTime.ChannelManager.Infrastructure.ExternalServices;
using TakeTime.ChannelManager.Application.Commands;
using TakeTime.Core.Extensions;
using TakeTime.MultiTenancy;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "ChannelManager")
        .WriteTo.Console();
});

// Add Core services (MediatR, FluentValidation, pipeline behaviors)
builder.Services.AddCore(typeof(TakeTime.ChannelManager.Application.Commands.SyncInventoryCommand).Assembly);

// Add multi-tenancy services
builder.Services.AddMultiTenancy(builder.Configuration);

// Register DbContext
builder.Services.AddDbContext<ChannelManagerDbContext>((serviceProvider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
});

// Register HTTP clients for OTA APIs
builder.Services.AddHttpClient("BookingCom");
builder.Services.AddHttpClient("Agoda");
builder.Services.AddHttpClient("Expedia");

// Register application repositories and services
builder.Services.AddScoped<IChannelManagerRepository, ChannelManagerRepository>();
builder.Services.AddScoped<IChannelApiClientFactory, ChannelApiClientFactory>();

// Add controllers
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TakeTime Channel Manager API",
        Version = "v1",
        Description = "API for managing OTA channel connections, inventory synchronization, and reservation imports."
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
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
            ValidAudience = builder.Configuration["Identity:Audience"] ?? "taketime-channelmanager-api"
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TakeTime Channel Manager API v1");
    });
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

Log.Information("TakeTime Channel Manager API starting up...");

app.Run();
