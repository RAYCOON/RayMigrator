# Exercise 0: Set up your machine (before the workshop, about 10 minutes)

Please finish these steps **before** the session. Every later exercise builds on them.

## What you need

| Item | Where from |
|---|---|
| .NET 10 runtime | <https://dotnet.microsoft.com/download/dotnet/10.0> (the runtime is enough, the SDK also works) |
| RayMigrator 0.15.0 | <https://github.com/RAYCOON/RayMigrator/releases/tag/v0.15.0> |
| SQL Server Management Studio (SSMS) | <https://aka.ms/ssms> |
| Server name and SQL login for the shared workshop instance | from the presenter |
| This `Workshop/` folder | `git clone --depth 1 https://github.com/RAYCOON/RayMigrator.git` or the ZIP download on GitHub |

## Step 1: .NET 10 runtime

Install it, then check:

```bash
dotnet --list-runtimes
```

The list must contain a line starting with `Microsoft.NETCore.App 10.`.

## Step 2: RayMigrator 0.15.0

1. Download the archive for your platform: `RayMigrator-0.15.0-win-x64.zip`, `RayMigrator-0.15.0-linux-x64.tar.gz` or `RayMigrator-0.15.0-osx-arm64.tar.gz`.
2. Extract the **whole** archive into a folder of its own, for example `C:\Tools\raymigrator` or `~/raymigrator`. The `DataAccessLayers/` folder must stay next to the executable; RayMigrator loads its database plugins from there.
3. Add that folder to your `PATH`.
4. macOS only: if Gatekeeper refuses to start the binary, run `xattr -d com.apple.quarantine ~/raymigrator/raymigrator` once.
5. Check:

```bash
raymigrator --version
```

Expected: `0.15.0+<commit hash>`.

## Step 3: Your personal database

1. Start SSMS and connect to the shared instance with the server name and login you received.
2. Open [create-database.sql](create-database.sql), replace `<NAME>` with your short name (letters and digits only, for example `BookStore_Daniel`), execute it.
3. Keep SSMS open; you will look into the database during the exercises.

RayMigrator never creates databases. It creates schemas and tables inside the database you give it.

## Step 4: Your personal configuration file

1. Open the folder `Workshop/exercises/bookstore/` in a terminal and in an editor.
2. Copy `appsettings.Workshop.template.json` to `appsettings.Workshop.json` (same folder).
3. Replace the placeholders:

| Placeholder | Value |
|---|---|
| `<SERVER>` | server name from the presenter, for example `sql.example.local,1433` |
| `<NAME>` | the suffix you used for your database |
| `<LOGIN>` / `<PASSWORD>` | your SQL login |
| `<ABSOLUTE_PATH>` | the absolute path of your clone, written with forward slashes, for example `C:/Users/daniel/RayMigrator` |

Both connection strings must be **byte-identical**; copy and paste them. The file is ignored by git because it contains your password.

## Step 5: Check

```bash
cd Workshop/exercises/bookstore
raymigrator migrate-up -p BookStore -env Workshop -rm validate
```

Expected (abridged):

```text
[INF] RayMigrator starting up in Standalone mode, environment [Workshop] ...
[WRN] Config warning [RULE_7_3] at Repository > ConnectionString: Connection string appears to contain a hardcoded credential. ...
[WRN] Config warning [RULE_7_3] at Products > BookStore > ... > MainDB > ConnectionString: ...
[WRN] Config warning [RULE_7_1] at Products > BookStore > ... > MainDB > ConnectionString: Repository ConnectionString is identical to Target 'MainDB' ...
 (RayMigrator banner)
[INF] Executing Migrate-Up for product BookStore in environment Workshop
[INF] No migration files found for product BookStore
```

The exit code is `0` (`echo $?` in bash, `$LASTEXITCODE` in PowerShell). The three warnings are expected in this workshop setup; the first session block explains them. Validate mode reads only your files and configuration, so it does not prove that the login works. That proof came from SSMS in step 3.

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `raymigrator: command not found` | The folder from step 2 is not on `PATH`, or the terminal was opened before you changed it. |
| `No 'Serilog' configuration found` or exit code 4 | You are not in `Workshop/exercises/bookstore/`. RayMigrator reads `appsettings*.json` from the current directory (or from `--config-dir`). |
| Exit code 3, "no environment" | `-env Workshop` is missing. |
| `... MigrationFilesRootDirectory ... does not exist` | `<ABSOLUTE_PATH>` is wrong. Relative paths resolve against the RayMigrator executable, not against your folder, so keep it absolute. |
| `Login failed for user` | Check `<LOGIN>`, `<PASSWORD>` and that you replaced the placeholders in **both** connection strings. |
| Certificate error (`The certificate chain was issued by an authority that is not trusted`) | Keep `TrustServerCertificate=True` in both connection strings. |
| `Cannot open database "BookStore_<NAME>"` | Step 3 was skipped, or `<NAME>` differs between SSMS and the JSON file. |
| Unicode or JSON errors | Save the JSON file as UTF-8, keep the quotes, use forward slashes in the path. |

Done? Then you are ready for [Exercise 1](../01-first-migration/README.md).
