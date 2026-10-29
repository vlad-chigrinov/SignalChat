using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SignalChat.Api.Tests.Infrastructure;

/// <summary>
/// Test-only model tweak for SQLite. SQLite cannot translate ORDER BY on DateTimeOffset (also inside correlated
/// subqueries), so every DateTimeOffset column is stored as a sortable long. PostgreSQL, the production
/// provider, keeps timestamptz; this customizer is registered only in the test host and never touches
/// the production model or its migrations.
/// </summary>
public sealed class SqliteDateTimeOffsetModelCustomizer(ModelCustomizerDependencies dependencies)
    : RelationalModelCustomizer(dependencies)
{
    private static readonly DateTimeOffsetToBinaryConverter SortableConverter = new();

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(SortableConverter);
                }
            }
        }
    }
}
