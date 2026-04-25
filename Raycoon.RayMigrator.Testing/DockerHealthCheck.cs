using Raycoon.RayMigrator.Database;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Testing;

/// <summary>
/// Checks whether Docker database containers are available before running tests.
/// </summary>
public static class DockerHealthCheck
{
    /// <summary>
    /// Attempts to connect to the specified database and returns true if reachable.
    /// </summary>
    public static bool IsDatabaseAvailable(string databaseType, string connectionString)
    {
        try
        {
            if (!DalFactory.TryGetDal(databaseType, connectionString, out IDal? dal))
                return false;

            dal!.CheckConnectionStringOrValidateConnection(true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
