using System.Data.Common;
using FC.Engine.Domain.Abstractions;
using FC.Engine.Infrastructure.MultiTenancy;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace FC.Engine.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core connection interceptor that sets SQL Server SESSION_CONTEXT('TenantId')
/// when a connection is opened. This enables Row-Level Security for all EF Core queries.
/// </summary>
public class TenantSessionContextInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantSessionContextInterceptor(
        ITenantContext tenantContext,
        IHttpContextAccessor httpContextAccessor)
    {
        _tenantContext = tenantContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = BuildSessionContextCommand(connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        using var cmd = BuildSessionContextCommand(connection);
        cmd.ExecuteNonQuery();
    }

    private DbCommand BuildSessionContextCommand(DbConnection connection)
    {
        var scope = TenantSessionContextScopeResolver.Resolve(_tenantContext, _httpContextAccessor);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            EXEC sp_set_session_context @key=N'TenantId', @value=@tid;
            EXEC sp_set_session_context @key=N'BypassRls', @value=@bypassRls;
            EXEC sp_set_session_context @key=N'TenantType', @value=@tenantType;
            EXEC sp_set_session_context @key=N'RegulatorCode', @value=@regulatorCode;";

        cmd.Parameters.Add(new SqlParameter("@tid", scope.TenantId ?? (object)DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@bypassRls", scope.BypassRls));
        cmd.Parameters.Add(new SqlParameter("@tenantType", scope.TenantType ?? (object)DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@regulatorCode", scope.RegulatorCode ?? (object)DBNull.Value));

        return cmd;
    }
}
