# Third-Party Notices

RayMigrator is licensed under the Business Source License 1.1 — see
[`LICENSE.md`](LICENSE.md). This file covers the third-party components
distributed with it.

The RayMigrator CLI is published as a self-extracting single-file binary that
carries its dependencies with it, so these components are redistributed as part
of every release. They remain under their own licenses; nothing in
`LICENSE.md` applies to them.

Package versions below are those resolved for the RayMigrator 0.11.0 CLI
(`net10.0`). License identifiers were read from the packages' own `.nuspec`
metadata. Actual license texts are available at the identifier URLs given in
each section, and are included in the respective NuGet packages.

---

## Apache License 2.0

<https://www.apache.org/licenses/LICENSE-2.0>

- Raycoon.Serilog.Sinks.SQLite 1.2.2
- Serilog 4.4.0
- Serilog.Enrichers.Environment 3.0.1
- Serilog.Enrichers.Thread 4.0.0
- Serilog.Extensions.Hosting 10.0.0
- Serilog.Extensions.Logging 10.0.0
- Serilog.Settings.Configuration 10.0.1
- Serilog.Sinks.Console 6.1.1
- Serilog.Sinks.File 7.0.0
- Serilog.Sinks.PeriodicBatching 5.0.0
- SQLitePCLRaw.bundle_e_sqlite3 2.1.12
- SQLitePCLRaw.core 2.1.12
- SQLitePCLRaw.lib.e_sqlite3 2.1.12
- SQLitePCLRaw.provider.e_sqlite3 2.1.12

The SQLitePCLRaw packages bundle the native SQLite library (`e_sqlite3`).
SQLite itself is in the public domain — <https://www.sqlite.org/copyright.html>.

---

## MIT License

<https://opensource.org/licenses/MIT>

- Microsoft.Bcl.Cryptography 9.0.13
- Microsoft.Data.SqlClient 7.0.2
- Microsoft.Data.SqlClient.Extensions.Abstractions 7.0.2
- Microsoft.Data.SqlClient.Internal.Logging 7.0.2
- Microsoft.Data.Sqlite 10.0.11
- Microsoft.Data.Sqlite.Core 10.0.11
- Microsoft.Extensions.Caching.Abstractions 9.0.13
- Microsoft.Extensions.Caching.Memory 9.0.13
- Microsoft.Extensions.Configuration 10.0.11
- Microsoft.Extensions.Configuration.Abstractions 10.0.11
- Microsoft.Extensions.Configuration.Binder 10.0.11
- Microsoft.Extensions.Configuration.CommandLine 10.0.11
- Microsoft.Extensions.Configuration.EnvironmentVariables 10.0.11
- Microsoft.Extensions.Configuration.FileExtensions 10.0.11
- Microsoft.Extensions.Configuration.Json 10.0.11
- Microsoft.Extensions.Configuration.UserSecrets 10.0.11
- Microsoft.Extensions.DependencyInjection 10.0.11
- Microsoft.Extensions.DependencyInjection.Abstractions 10.0.11
- Microsoft.Extensions.DependencyModel 10.0.11
- Microsoft.Extensions.Diagnostics 10.0.11
- Microsoft.Extensions.Diagnostics.Abstractions 10.0.11
- Microsoft.Extensions.FileProviders.Abstractions 10.0.11
- Microsoft.Extensions.FileProviders.Physical 10.0.11
- Microsoft.Extensions.FileSystemGlobbing 10.0.11
- Microsoft.Extensions.Hosting 10.0.11
- Microsoft.Extensions.Hosting.Abstractions 10.0.11
- Microsoft.Extensions.Logging 10.0.11
- Microsoft.Extensions.Logging.Abstractions 10.0.11
- Microsoft.Extensions.Logging.Configuration 10.0.11
- Microsoft.Extensions.Logging.Console 10.0.11
- Microsoft.Extensions.Logging.Debug 10.0.11
- Microsoft.Extensions.Logging.EventLog 10.0.11
- Microsoft.Extensions.Logging.EventSource 10.0.11
- Microsoft.Extensions.Options 10.0.11
- Microsoft.Extensions.Options.ConfigurationExtensions 10.0.11
- Microsoft.Extensions.Options.DataAnnotations 10.0.11
- Microsoft.Extensions.Primitives 10.0.11
- Microsoft.IdentityModel.Abstractions 8.16.0
- Microsoft.IdentityModel.JsonWebTokens 8.16.0
- Microsoft.IdentityModel.Logging 8.16.0
- Microsoft.IdentityModel.Protocols 8.16.0
- Microsoft.IdentityModel.Protocols.OpenIdConnect 8.16.0
- Microsoft.IdentityModel.Tokens 8.16.0
- Microsoft.SqlServer.Server 1.0.0
- MySqlConnector 2.6.2
- System.CommandLine 2.0.11
- System.Configuration.ConfigurationManager 9.0.13
- System.Diagnostics.EventLog 10.0.11
- System.IdentityModel.Tokens.Jwt 8.16.0
- System.Security.Cryptography.Pkcs 9.0.13
- System.Security.Cryptography.ProtectedData 9.0.13

---

## PostgreSQL License

<https://opensource.org/licenses/postgresql>

- Npgsql 10.0.3

---

## Microsoft Software License Terms

- Microsoft.Data.SqlClient.SNI.runtime 6.0.2 —
  <https://aka.ms/sqlclientproject>

This is the native SNI layer used by `Microsoft.Data.SqlClient` on Windows and
is present only in Windows publish output. It is not open source. Microsoft
permits redistribution as "Distributable Code" as part of an application,
subject to conditions including: use within an application rather than as a
standalone distribution, passing on at least equivalent protective terms to
downstream distributors, and indemnifying Microsoft in respect of the
distribution of the application. The full terms ship in the package as
`LICENSE.txt`.

---

## Build and test dependencies

The following are used to build and test RayMigrator but are **not**
redistributed in any release artifact:

- AwesomeAssertions 9.5.0 — Apache License 2.0
- xunit.v3 3.2.2, xunit.runner.visualstudio 3.1.5 — Apache License 2.0
- NSubstitute 5.3.0 — BSD 3-Clause
- Microsoft.NET.Test.Sdk 18.3.0, coverlet.collector 8.0.1 — MIT
- MudBlazor 9.2.0, Microsoft.AspNetCore.Components.WebAssembly 10.0.11 — MIT
  (used by the Config Wizard web app, deployed separately from the CLI)

---

## Maintaining this file

Regenerate the package list whenever dependencies change:

```bash
dotnet list Raycoon.RayMigrator.Console/Raycoon.RayMigrator.Console.csproj \
    package --include-transitive --framework net10.0
```

License identifiers come from each package's `.nuspec` in the local NuGet cache
(`~/.nuget/packages/<id>/<version>/<id>.nuspec`), element `<license>`. Any
package resolving to something other than MIT or Apache-2.0 deserves a read of
its actual terms before shipping, as the SNI entry above illustrates.
