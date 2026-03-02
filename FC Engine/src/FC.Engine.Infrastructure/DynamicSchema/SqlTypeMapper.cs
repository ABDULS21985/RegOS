using FC.Engine.Domain.Abstractions;
using FC.Engine.Domain.Enums;

namespace FC.Engine.Infrastructure.DynamicSchema;

public class SqlTypeMapper : ISqlTypeMapper
{
    public string MapToSqlType(FieldDataType dataType, string? sqlTypeOverride = null)
    {
        if (!string.IsNullOrEmpty(sqlTypeOverride))
            return sqlTypeOverride;

        return dataType switch
        {
            FieldDataType.Money => "DECIMAL(20,2)",
            FieldDataType.Decimal => "DECIMAL(20,4)",
            FieldDataType.Percentage => "DECIMAL(10,4)",
            FieldDataType.Integer => "INT",
            FieldDataType.Text => "NVARCHAR(255)",
            FieldDataType.Date => "DATE",
            FieldDataType.Boolean => "BIT",
            _ => "NVARCHAR(255)"
        };
    }
}
