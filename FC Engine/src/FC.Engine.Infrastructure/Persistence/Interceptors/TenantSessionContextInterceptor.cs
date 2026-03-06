using System.Data.Common;
using FC.Engine.Domain.Abstractions;
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
        var httpContext = _httpContextAccessor.HttpContext;
        var tenantType = ResolveSessionValue(httpContext?.Items["TenantType"], httpContext?.User?.FindFirst("TenantType")?.Value);
        var regulatorCode = ResolveSessionValue(httpContext?.Items["RegulatorCode"], httpContext?.User?.FindFirst("RegulatorCode")?.Value);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            EXEC sp_set_session_context @key=N'TenantId', @value=@tid;
            EXEC sp_set_session_context @key=N'TenantType', @value=@tenantType;
            EXEC sp_set_session_context @key=N'RegulatorCode', @value=@regulatorCode;";

        cmd.Parameters.Add(new SqlParameter("@tid", _tenantContext.CurrentTenantId.HasValue
            ? _tenantContext.CurrentTenantId.Value
            : DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@tenantType", tenantType ?? (object)DBNull.Value));
        cmd.Parameters.Add(new SqlParameter("@regulatorCode", regulatorCode ?? (object)DBNull.Value));

        return cmd;
    }

    private static string? ResolveSessionValue(object? itemValue, string? claimValue)
    {
        if (itemValue is string s && !string.IsNullOrWhiteSpace(s))
        {
            return s;
        }

        if (!string.IsNullOrWhiteSpace(claimValue))
        {
            return claimValue;
        }

        return null;
    }
}
