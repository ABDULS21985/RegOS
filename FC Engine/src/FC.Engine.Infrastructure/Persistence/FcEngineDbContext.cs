using FC.Engine.Domain.Entities;
using FC.Engine.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.Persistence;

public class FcEngineDbContext : DbContext
{
    public FcEngineDbContext(DbContextOptions<FcEngineDbContext> options) : base(options) { }

    // Reference tables
    public DbSet<Institution> Institutions => Set<Institution>();
    public DbSet<ReturnPeriod> ReturnPeriods => Set<ReturnPeriod>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<ValidationReport> ValidationReports => Set<ValidationReport>();
    public DbSet<ValidationError> ValidationErrors => Set<ValidationError>();

    // Return data tables
    public DbSet<Mfcr300Entity> Mfcr300 => Set<Mfcr300Entity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FcEngineDbContext).Assembly);
    }
}
