namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The tables of a scenario's table prefix, which the scenario drops when it ends: the repository and log tables of the
/// prefix in every database of <c>ConnectionStrings</c>, the repository database and a log database of its own. The
/// cleanup before a scenario drops the repository tables of that scenario's prefix; on SQL Server and PostgreSQL it drops
/// the whole test schema and with it every other prefix, on MySQL, MariaDB and SQLite the tables of other prefixes stay.
/// Without this cleanup the tables of a prefix that no later scenario uses would stay in the MySQL and MariaDB databases
/// (a SQLite file is deleted with its fixture). A scenario without a prefix drops nothing when it ends: its tables stay
/// for inspection, and the next scenario drops them before it starts.
/// </summary>
internal sealed record PrefixTableCleanup(
    string DatabaseType,
    string SchemaName,
    string TableBaseName,
    IReadOnlyList<string> ConnectionStrings)
{
    /// <summary>
    /// Drops the tables of the prefix in every database and never throws: it runs in
    /// <see cref="ScenarioContext.DisposeAsync"/>, where an exception would replace the exception of a failing test. A
    /// table that stays is dropped by the next scenario with the same prefix before it starts.
    /// </summary>
    public void Run()
    {
        if (TableBaseName.Length == 0)
            return;

        foreach (string connectionString in ConnectionStrings)
        {
            try
            {
                DatabaseCleanupHelper.CleanDatabase(DatabaseType, connectionString, SchemaName, TableBaseName);
            }
            catch
            {
                // ignored: best-effort clean-up that must not hide the result of the test (see the summary)
            }
        }
    }
}
