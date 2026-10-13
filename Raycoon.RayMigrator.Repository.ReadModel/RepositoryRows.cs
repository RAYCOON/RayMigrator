using System.Collections.Concurrent;
using System.Reflection;

namespace Raycoon.RayMigrator.Repository.ReadModel;

/// <summary>
/// The record type of every table and, derived from its primary constructor, the columns of the table: a parameter
/// name is the column name in PascalCase, the parameter type the .NET type of the value. The record types are the
/// single source of the column contract, so they cannot drift from the lists a reader builds its statements from.
/// </summary>
public static class RepositoryRows
{
    private static readonly ConcurrentDictionary<Type, ConstructorInfo> Constructors = new();
    private static readonly ConcurrentDictionary<RepositoryTable, IReadOnlyList<string>> Columns = new();

    /// <summary>The record type of the rows of <paramref name="table"/>.</summary>
    public static Type TypeOf(RepositoryTable table) => table switch
    {
        RepositoryTable.RepositorySchemaVersion => typeof(RepositorySchemaVersionRow),
        RepositoryTable.MigratorMeta => typeof(MigratorMetaRow),
        RepositoryTable.Product => typeof(ProductRow),
        RepositoryTable.Environment => typeof(EnvironmentRow),
        RepositoryTable.MigrationRun => typeof(MigrationRunRow),
        RepositoryTable.MigrationRunMeta => typeof(MigrationRunMetaRow),
        RepositoryTable.MigrationRecord => typeof(MigrationRecordRow),
        RepositoryTable.MigrationRecordHistory => typeof(MigrationRecordHistoryRow),
        RepositoryTable.MigrationRunMode => typeof(LookupRow),
        RepositoryTable.MigrationOperation => typeof(LookupRow),
        RepositoryTable.MigrationRunResult => typeof(LookupRow),
        RepositoryTable.MigrationStatus => typeof(LookupRow),
        RepositoryTable.MigrationRunSource => typeof(LookupRow),
        RepositoryTable.MigrationEvent => typeof(MigrationEventRow),
        RepositoryTable.MigrationLog => typeof(MigrationLogRow),
        _ => throw new ArgumentOutOfRangeException(nameof(table), table, "Unknown repository table."),
    };

    /// <summary>The columns of <paramref name="table"/> in PascalCase, in the order of the record's constructor.</summary>
    public static IReadOnlyList<string> ColumnsOf(RepositoryTable table) =>
        Columns.GetOrAdd(table, t => ConstructorOf(TypeOf(t)).GetParameters().Select(p => p.Name!).ToArray());

    /// <summary>The primary constructor of a row record: the public constructor with the most parameters.</summary>
    /// <exception cref="ArgumentException"><paramref name="rowType"/> has no public constructor with parameters.</exception>
    public static ConstructorInfo ConstructorOf(Type rowType) =>
        Constructors.GetOrAdd(rowType, t =>
            t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Where(c => c.GetParameters().Length > 0)
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault()
            ?? throw new ArgumentException($"[{t.Name}] is not a row record: it has no public constructor with parameters.", nameof(rowType)));
}
