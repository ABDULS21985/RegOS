using System.Data;
using Dapper;
using FC.Engine.Domain.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace FC.Engine.Infrastructure.MultiTenancy;

/// <summary>
/// Creates database connections with SQL Server SESSION_CONTEXT('TenantId') set.
/// This enables Row-Level Security filtering at the database engine level.
/// </summary>
public class TenantAwareConnectionFactory : IDbConnectionFactory
{
    private readonly IDataResidencyRouter _dataResidencyRouter;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantAwareConnectionFactory(
        IDataResidencyRouter dataResidencyRouter,
        IHttpContextAccessor httpContextAccessor)
    {
        _dataResidencyRouter = dataResidencyRouter;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IDbConnection> CreateConnectionAsync(Guid? tenantId, CancellationToken ct = default)
    {
        var connectionString = await _dataResidencyRouter.ResolveConnectionString(tenantId, ct);
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var scope = TenantSessionContextScopeResolver.Resolve(tenantId, _httpContextAccessor);

        // Always reset all session keys on open to avoid stale pooled-connection state.
        await connection.ExecuteAsync(
            @"EXEC sp_set_session_context @key=N'TenantId', @value=@tenantId;
              EXEC sp_set_session_context @key=N'BypassRls', @value=@bypassRls;
              EXEC sp_set_session_context @key=N'TenantType', @value=@tenantType;
              EXEC sp_set_session_context @key=N'RegulatorCode', @value=@regulatorCode;",
            new
            {
                tenantId = scope.TenantId,
                bypassRls = scope.BypassRls,
                tenantType = scope.TenantType,
                regulatorCode = scope.RegulatorCode
            });

        return connection;
    }
}
