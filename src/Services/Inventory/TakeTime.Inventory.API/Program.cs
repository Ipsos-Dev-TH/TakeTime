using Microsoft.EntityFrameworkCore;
using Serilog;
using TakeTime.Inventory.Infrastructure.Repositories;
using TakeTime.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "Inventory")
        .WriteTo.Console();
});

// Add Core services (MediatR, FluentValidation, pipeline behaviors)
builder.Services.AddCore(typeof(TakeTime.Inventory.Application.Commands.AdjustStockCommand).Assembly);

// Register DbContext
builder.Services.AddDbContext<InventoryDbContext>((serviceProvider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        options.UseNpgsql(connectionString);
    }
});

// Add controllers
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TakeTime Inventory API",
        Version = "v1",
        Description = "API for product management, POS transactions, stock tracking, and inventory control."
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
            ValidAudience = builder.Configuration["Identity:Audience"] ?? "taketime-inventory-api"
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
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "TakeTime Inventory API v1");
    });
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

Log.Information("TakeTime Inventory API starting up...");

app.Run();
