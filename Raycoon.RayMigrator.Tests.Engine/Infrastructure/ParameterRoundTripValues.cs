using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Tests.Engine.Infrastructure;

/// <summary>
/// The values the parameter round-trip tests of the five DALs send through a template parameter: a string with
/// every character that could break the SQL text when a DAL substitutes the value into it (quotes, backslashes,
/// the PostgreSQL DO-block delimiter, other placeholders, comment and statement markers, non-ASCII text), next
/// to a second string, an integer and a null.
/// </summary>
internal static class ParameterRoundTripValues
{
    public const string HostileString =
        "it's \"quoted\" \\back\\slash $$ @Value @Other @Id $rm$ -- ; DROP TABLE nothing; /* c */ ünïcödé 🚀";

    public static readonly DalSettings Settings = new()
    {
        UseTransaction = false,
        DbCommandTimeoutInSeconds = 30,
        MaxRetries = 0,
    };

    public static DalParameterList Parameters()
    {
        var parameters = new DalParameterList();
        parameters.AddParameter(new DalParameter("Value", HostileString, typeof(string)));
        parameters.AddParameter(new DalParameter("Other", "other", typeof(string)));
        parameters.AddParameter(new DalParameter("Id", 42, typeof(int)));
        parameters.AddParameter(new DalParameter("Null", null, typeof(string)));
        return parameters;
    }
}
