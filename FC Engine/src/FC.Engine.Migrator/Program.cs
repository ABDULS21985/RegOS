using FC.Engine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var connectionString = context.Configuration.GetSection("ConnectionStrings")["FcEngine"]
            ?? throw new InvalidOperationException("Connection string 'FcEngine' not found.");

        services.AddDbContext<FcEngineDbContext>(options =>
            options.UseSqlServer(connectionString));
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Starting database migration...");

    using var scope = host.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FcEngineDbContext>();

    logger.LogInformation("Applying migrations...");
    await db.Database.MigrateAsync();

    logger.LogInformation("Database migration completed successfully.");
}
catch (Exception ex)
{
    logger.LogError(ex, "An error occurred while migrating the database.");
    throw;
}
