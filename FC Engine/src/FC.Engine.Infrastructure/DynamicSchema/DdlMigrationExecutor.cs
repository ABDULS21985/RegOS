using System.Diagnostics;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Infrastructure.Metadata;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FC.Engine.Infrastructure.DynamicSchema;

public class DdlMigrationExecutor : IDdlMigrationExecutor
{
    private readonly MetadataDbContext _db;

    public DdlMigrationExecutor(MetadataDbContext db) => _db = db;

    public async Task<MigrationResult> Execute(
        int templateId,
        int? versionFrom,
        int versionTo,
        DdlScript ddlScript,
        string executedBy,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ddlScript.ForwardSql))
            return new MigrationResult(true, null);

        var sw = Stopwatch.StartNew();

        try
        {
            // Execute DDL using raw SQL (outside EF transaction for DDL)
            var connection = _db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(ct);

            using var command = connection.CreateCommand();
            command.CommandText = ddlScript.ForwardSql;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(ct);

            sw.Stop();

            // Record migration
            var migration = new DdlMigrationRecord
            {
                TemplateId = templateId,
                VersionFrom = versionFrom,
                VersionTo = versionTo,
                MigrationType = versionFrom == null ? "CreateTable" : "AlterTable",
                DdlScript = ddlScript.ForwardSql,
                RollbackScript = ddlScript.RollbackSql,
                ExecutedAt = DateTime.UtcNow,
                ExecutedBy = executedBy,
                ExecutionDurationMs = (int)sw.ElapsedMilliseconds
            };

            _db.DdlMigrations.Add(migration);
            await _db.SaveChangesAsync(ct);

            return new MigrationResult(true, null);
        }
        catch (SqlException ex)
        {
            return new MigrationResult(false, ex.Message);
        }
    }

    public async Task<MigrationResult> Rollback(int migrationId, string rolledBackBy, CancellationToken ct = default)
    {
        var migration = await _db.DdlMigrations.FindAsync(new object[] { migrationId }, ct);
        if (migration == null)
            return new MigrationResult(false, $"Migration {migrationId} not found");

        if (migration.IsRolledBack)
            return new MigrationResult(false, "Migration already rolled back");

        try
        {
            var connection = _db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(ct);

            using var command = connection.CreateCommand();
            command.CommandText = migration.RollbackScript;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(ct);

            migration.IsRolledBack = true;
            migration.RolledBackAt = DateTime.UtcNow;
            migration.RolledBackBy = rolledBackBy;
            await _db.SaveChangesAsync(ct);

            return new MigrationResult(true, null);
        }
        catch (SqlException ex)
        {
            return new MigrationResult(false, ex.Message);
        }
    }
}
