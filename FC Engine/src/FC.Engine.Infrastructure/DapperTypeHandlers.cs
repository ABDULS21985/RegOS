using System.Data;
using System.Runtime.CompilerServices;
using Dapper;

namespace FC.Engine.Infrastructure;

internal static class DapperTypeHandlers
{
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Register()
    {
        SqlMapper.AddTypeMap(typeof(DateOnly), DbType.Date);
        SqlMapper.AddTypeMap(typeof(DateOnly?), DbType.Date);
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new NullableDateOnlyTypeHandler());
    }
#pragma warning restore CA2255

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.ToDateTime(TimeOnly.MinValue);
        }

        public override DateOnly Parse(object value)
        {
            return value switch
            {
                DateOnly dateOnly => dateOnly,
                DateTime dateTime => DateOnly.FromDateTime(dateTime),
                DateTimeOffset dateTimeOffset => DateOnly.FromDateTime(dateTimeOffset.UtcDateTime),
                string text when DateOnly.TryParse(text, out var parsed) => parsed,
                _ => throw new DataException($"Cannot convert {value.GetType().FullName} to DateOnly.")
            };
        }
    }

    private sealed class NullableDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly?>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly? value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value.HasValue
                ? value.Value.ToDateTime(TimeOnly.MinValue)
                : DBNull.Value;
        }

        public override DateOnly? Parse(object value)
        {
            return value switch
            {
                null or DBNull => null,
                DateOnly dateOnly => dateOnly,
                DateTime dateTime => DateOnly.FromDateTime(dateTime),
                DateTimeOffset dateTimeOffset => DateOnly.FromDateTime(dateTimeOffset.UtcDateTime),
                string text when DateOnly.TryParse(text, out var parsed) => parsed,
                _ => throw new DataException($"Cannot convert {value.GetType().FullName} to DateOnly?.")
            };
        }
    }
}
