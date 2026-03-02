using FC.Engine.Api.Endpoints;
using FC.Engine.Api.Middleware;
using FC.Engine.Application;
using FC.Engine.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Services
var connectionString = builder.Configuration.GetConnectionString("FcEngine")
    ?? throw new InvalidOperationException("Connection string 'FcEngine' not found.");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddApplication();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "FC Engine API",
        Version = "v1",
        Description = "CBN DFIS Finance Company Returns Data Processing Engine"
    });
});

var app = builder.Build();

// Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithTags("Health");

// Endpoints
app.MapSubmissionEndpoints();
app.MapSchemaEndpoints();

app.Run();

// Required for integration test WebApplicationFactory
public partial class Program { }
