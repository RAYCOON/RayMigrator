# Adding a New CLI Command

Step-by-step guide for implementing a new command in RayMigrator CLI.

## Overview

RayMigrator uses `System.CommandLine` for CLI command handling. Commands are defined as factory methods in `CommandLineConfiguration` (`Raycoon.RayMigrator.Core/Configuration/Options/CommandLineConfiguration.cs`), with handlers that populate a shared `RayMigratorConsoleOptions` object. The `RayMigratorService` (`Raycoon.RayMigrator.Pipeline/RayMigratorService.cs`) then dispatches to the appropriate service method via a switch statement. Migration command handlers receive the global options (`showInfoOption`, `revealSensitiveDataOption`, `configDirOption`) to control startup info, sensitive data display, and configuration directory override. The global options are named `--startup-info` (`-si`), `--reveal-sensitive-data` (`-rsd`), and `--config-dir` (`-cd`).

Adding a new command requires changes in these layers:

1. **Core**: Add enum value, create command factory method and handler setup in `CommandLineConfiguration`
2. **Services.Abstractions**: Add interface method and request/response types
3. **Services**: Implement the service method
4. **Pipeline**: Add bridge method in `RayMigratorService`

## Prerequisites

- Understanding of System.CommandLine library
- Familiarity with the service layer architecture
- Knowledge of dependency injection patterns

## Example: Adding a Migrate-Status Command

We'll implement a `Migrate-Status` command that shows the current migration state.

## Step 1: Add the Command Enum Value

Add a new value to the `MigrationCommand` enum:

```csharp
// Raycoon.RayMigrator.Core/Configuration/Enums/MigrationCommand.cs
public enum MigrationCommand : byte
{
    None = 0,
    MigrateUp = 1,
    MigrateDown = 2,
    ValidateHash = 3,
    UpdateHash = 4,
    Info = 5,
    Baseline = 6,
    FixIssues = 7,
    MigrateStatus = 8    // new value (highest existing value is FixIssues = 7)
}
```

## Step 2: Define the Service Interface

### Create Request/Response Classes

Request and response classes live in `Raycoon.RayMigrator.Services.Abstractions/Models/`. Requests go in `Requests.cs`, results go in `Results.cs`.

```csharp
// In Raycoon.RayMigrator.Services.Abstractions/Models/Requests.cs
public class MigrateStatusRequest
{
    public string ProductAlias { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string[]? TargetGroupAliases { get; set; }
    public bool ShowDetails { get; set; } = false;
    public bool ShowInfo { get; set; } = true;
    public bool RevealSensitiveData { get; set; } = false;
}
```

All result classes inherit from the `OperationResult` base class which provides `Success`, `ErrorMessage`, `ErrorCode`, `Messages`, `ExecutedAt`, and `Duration`:

```csharp
// In Raycoon.RayMigrator.Services.Abstractions/Models/Results.cs
public class MigrateStatusResponse : OperationResult
{
    public IReadOnlyList<TargetGroupStatus> TargetGroups { get; set; } = [];
}
```

### Add Interface Method

The existing `IMigrationService` interface uses typed request/response objects:

```csharp
// Raycoon.RayMigrator.Services.Abstractions/IMigrationService.cs
public interface IMigrationService
{
    // Existing methods (request/response pattern)
    Task<MigrationOperationResult> MigrateUpAsync(MigrateUpRequest request);
    Task<MigrationOperationResult> MigrateDownAsync(MigrateDownRequest request);
    Task<ValidationResult> ValidateHashAsync(ValidateHashRequest request);
    Task<HashUpdateResult> UpdateHashAsync(UpdateHashRequest request);
    Task<BaselineResult> BaselineAsync(BaselineRequest request);
    Task<FixIssuesResult> FixIssuesAsync(FixIssuesRequest request);

    // Existing methods (simple parameters, used by Info command)
    Task<MigrationStatusInfo> GetStatusAsync(string productAlias);
    Task<MigrationHistory> GetHistoryAsync(string productAlias, int limit = 100);

    // New method
    Task<MigrateStatusResponse> MigrateStatusAsync(MigrateStatusRequest request);
}
```

> **Note**: Existing methods like `GetStatusAsync` and `GetHistoryAsync` take simple parameters (not request objects) because they were designed for the `Info` command. New commands should follow the request/response pattern used by `MigrateUpAsync`, `MigrateDownAsync`, etc. All request classes should include `ShowInfo` and `RevealSensitiveData` properties. Most migration requests also include `TargetGroupAliases` (`string[]?`) for target group filtering (exception: `FixIssuesRequest` does not include `TargetGroupAliases`). `MigrateUpRequest` also includes `AllowOutOfOrder` (`bool`) for out-of-order migration execution and `TargetGroupMigrationOrder` (`string[]?`) for explicit target group ordering — both specific to that command. `BaselineRequest` also includes `TargetGroupMigrationOrder`. `FixIssuesRequest` includes `OlderThanMinutes`, `DryRun`, `AssumedMigrationStatus`, and `Scope` properties, which are populated from `_consoleOptions` in `ExecuteFixIssuesAsync()`. Some CLI options are not propagated via request classes but are read directly from `RayMigratorConsoleOptions` by the service (e.g., `StopRollbackOnMissingRollbackFile` for `Migrate-Up`); these options are typically also configurable in `appsettings.json` and the CLI value acts as an override.

## Step 3: Implement the Service Method

```csharp
// Raycoon.RayMigrator.Services/MigrationService.cs
public async Task<MigrateStatusResponse> MigrateStatusAsync(MigrateStatusRequest request)
{
    try
    {
        _logger.LogInformation("Getting migration status for {Product}/{Environment}",
            request.ProductAlias, request.Environment);

        // Implementation using _ctxAccessor.Current, _templateExecutor, etc.
        // ...

        return new MigrateStatusResponse
        {
            Success = true,       // inherited from OperationResult
            TargetGroups = targetGroups
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to get migration status");
        return new MigrateStatusResponse
        {
            Success = false,      // inherited from OperationResult
            ErrorMessage = ex.Message
        };
    }
}
```

## Step 4: Add Command to CommandLineConfiguration

All CLI command definitions live in `CommandLineConfiguration.cs` (`Raycoon.RayMigrator.Core/Configuration/Options/`). Each command uses two methods: a `Create*Command()` factory and a `Setup*Handler()` method.

### Add Factory Method

Create the command with its options using the same pattern as existing commands:

```csharp
// In CommandLineConfiguration.cs
private Command CreateMigrateStatusCommand()
{
    var command = new Command("Migrate-Status", "Show migration status for a product");

    var productOption = new Option<string>("--product", "-p")
    {
        Description = "Product alias from configuration",
        Required = true
    };

    var environmentOption = new Option<string>("--environment", "-env")
    {
        Description = "Target environment",
        Required = true
    };

    var targetGroupOption = new Option<string[]>("--target-group", "-tg")
    {
        Description = "Filter execution to specific target groups (can be specified multiple times)",
        Arity = ArgumentArity.ZeroOrMore
    };

    command.Options.Add(productOption);
    command.Options.Add(environmentOption);
    command.Options.Add(targetGroupOption);

    return command;
}
```

### Add Handler Setup Method

The handler uses `SetAction()` to parse CLI arguments into `ParsedOptions`. Handler setup methods for migration commands (MigrateUp, MigrateDown, ValidateHash, UpdateHash, Info, Baseline, FixIssues) receive the global options (`showInfoOption`, `revealSensitiveDataOption`, `configDirOption`):

```csharp
// In CommandLineConfiguration.cs
private void SetupMigrateStatusHandler(Command command, Option<bool> showInfoOption, Option<bool> revealSensitiveDataOption, Option<string?> configDirOption)
{
    command.SetAction(parseResult =>
    {
        ParsedOptions = new RayMigratorConsoleOptions
        {
            Command = MigrationCommand.MigrateStatus,
            Product = ResolveEnvironmentVariable(parseResult.GetValue(
                command.Options.OfType<Option<string>>().First(o => o.Name == "--product"))!),
            Environment = ResolveEnvironmentVariable(parseResult.GetValue(
                command.Options.OfType<Option<string>>().First(o => o.Name == "--environment"))!),
            RunMode = MigrationRunMode.Migrate,
            TargetReleaseVersion = null,
            ShowStartupInfo = parseResult.GetValue(showInfoOption),
            RevealSensitiveData = parseResult.GetValue(revealSensitiveDataOption),
            HashValidationScope = null,
            TargetGroupAliases = parseResult.GetValue(command.Options.OfType<Option<string[]>>()
                .First(o => o.Name == "--target-group"))
                ?.Select(a => ResolveEnvironmentVariable(a))
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .ToArray(),
            ConfigDir = ResolveConfigDir(parseResult.GetValue(configDirOption)),
        };
    });
}
```

> **Key Pattern**: The `SetAction()` lambda populates the `ParsedOptions` property. It does NOT execute the command -- that happens later in `RayMigratorService.DoWorkAsync()`. This separation allows `Program.cs` to perform environment resolution, configuration loading via `IOptionsSource`, DI setup, and validation between parsing and execution.

### Register in Constructor

Add the new command to the constructor alongside existing commands. The global options are local variables created at the top of the constructor:

```csharp
// In CommandLineConfiguration constructor — create and add the command
var migrateStatusCommand = CreateMigrateStatusCommand();
RootCommand.Subcommands.Add(migrateStatusCommand);

// Wire up the handler (alongside existing SetupXxxHandler calls)
SetupMigrateStatusHandler(migrateStatusCommand, showInfoOption, revealSensitiveDataOption, configDirOption);
```

## Step 5: Add Bridge Method in RayMigratorService

`RayMigratorService` (`Raycoon.RayMigrator.Pipeline/RayMigratorService.cs`) acts as the bridge between CLI parsing and service execution. It receives `ILogger<RayMigratorService>`, `RayMigratorConsoleOptions`, and `IMigrationService` via constructor injection. Add a switch case and an execute method:

### Add Switch Case

The `DoWorkAsync(IHost host)` method wraps the switch in a try/catch that handles `MigrationAlreadyRunningException` (suggesting the Fix command) and general exceptions:

```csharp
// In RayMigratorService.DoWorkAsync()
switch (_consoleOptions.Command)
{
    case MigrationCommand.MigrateUp:
        return await ExecuteMigrateUpAsync();
    case MigrationCommand.MigrateDown:
        return await ExecuteMigrateDownAsync();
    case MigrationCommand.ValidateHash:
        return await ExecuteValidateHashAsync();
    case MigrationCommand.UpdateHash:
        return await ExecuteUpdateHashAsync();
    case MigrationCommand.Info:
        return await ExecuteInfoAsync();
    case MigrationCommand.Baseline:
        return await ExecuteBaselineAsync();
    case MigrationCommand.FixIssues:
        return await ExecuteFixIssuesAsync();
    case MigrationCommand.MigrateStatus:          // New case
        return await ExecuteMigrateStatusAsync();
    default:
        throw new ConfigurationValidationException(
            $"Unknown command: {_consoleOptions.Command}. Valid commands: {string.Join(", ", Enum.GetNames<MigrationCommand>())}");
}
```

The `DoWorkAsync()` try/catch handles `MigrationAlreadyRunningException` (suggesting the Fix command) and general exceptions.

> **Note**: All commands that require the full DI container and migration context (MigrateUp, MigrateDown, ValidateHash, UpdateHash, Info, Baseline, FixIssues) go through `DoWorkAsync()`.

### Add Execute Method

Execute methods are private, parameterless, and read from the injected `_consoleOptions`:

```csharp
// In RayMigratorService
private async Task<int> ExecuteMigrateStatusAsync()
{
    _logger.LogDebug("Executing Migrate-Status command for product {Product} in environment {Environment}",
        _consoleOptions.Product, _consoleOptions.Environment);

    var request = new MigrateStatusRequest
    {
        ProductAlias = _consoleOptions.Product!,
        Environment = _consoleOptions.Environment!,
        ShowInfo = _consoleOptions.ShowStartupInfo,
        RevealSensitiveData = _consoleOptions.RevealSensitiveData,
        TargetGroupAliases = _consoleOptions.TargetGroupAliases,
    };

    var result = await _migrationService.MigrateStatusAsync(request);

    if (!result.Success)
    {
        _logger.LogError("Migrate-Status failed for product {Product}: {Error}",
            _consoleOptions.Product, result.ErrorMessage);
        return 1;
    }

    _logger.LogInformation("Migrate-Status completed for product {Product}", _consoleOptions.Product);
    return 0;
}
```

> **Pattern**: All `Execute*Async()` methods follow the same structure: log the start at Debug level, build a request from `_consoleOptions` (mapping `_consoleOptions.ShowStartupInfo` to `request.ShowInfo`, `_consoleOptions.RevealSensitiveData` to `request.RevealSensitiveData`, and `_consoleOptions.TargetGroupAliases` to `request.TargetGroupAliases`), call `_migrationService`, check `result.Success`, return `0` for success or `1` for failure.

## Step 6: Add Tests

### Unit Tests

```csharp
// Using xUnit, FluentAssertions, NSubstitute (project test stack)
public class MigrateStatusTests
{
    [Fact]
    public async Task MigrateStatusAsync_ValidProduct_ReturnsSuccess()
    {
        // Arrange
        var mockService = Substitute.For<IMigrationService>();
        mockService.MigrateStatusAsync(Arg.Any<MigrateStatusRequest>())
            .Returns(new MigrateStatusResponse
            {
                Success = true,  // inherited from OperationResult
                TargetGroups = new List<TargetGroupStatus>()
            });

        // Act
        var result = await mockService.MigrateStatusAsync(new MigrateStatusRequest
        {
            ProductAlias = "TestProduct",
            Environment = "Development"
        });

        // Assert
        result.Success.Should().BeTrue();
    }
}
```

## Step 7: Update Documentation

Add CLI reference documentation in `Docs/08-cli-reference/`:

```markdown
# Migrate-Status Command

Shows the current migration status for a product.

## Synopsis

RayMigrator Migrate-Status --product <ProductAlias> --environment <Environment> [options]

## Options

| Parameter | Short | Required | Description |
|-----------|-------|----------|-------------|
| --product | -p | Yes | Product alias |
| --environment | -env | Yes | Target environment |
| --target-group | -tg | No | Filter execution to specific target groups (repeatable) |
```

## Architecture Summary

The flow for any command follows this path:

```
CLI args → CommandLineConfiguration.SetAction() → ParsedOptions
         → EnvironmentResolver.Resolve() → environment + origin
         → RunDirectMode(JsonOptionsSource) → IOptionsSource.LoadAsync()
         → DirectModePipeline.ExecuteAsync()
         → Serilog init, DI host build, config validation
         → RayMigratorService.DoWorkAsync() → switch on Command
         → Execute*Async() → builds request from _consoleOptions
         → IMigrationService method → returns result
         → exit code 0 (success) or 1 (failure)
```

All migration commands (Migrate-Up, Migrate-Down, Validate-Hash, Update-Hash, Info, Baseline, Fix) go through `DoWorkAsync()`. The CLI command name for the FixIssues enum value is `Fix` (not `Fix-Issues`).

## Checklist

- [ ] Add value to `MigrationCommand` enum (Core)
- [ ] Create request/response classes in `Models/` (Services.Abstractions)
- [ ] Add method to `IMigrationService` interface (Services.Abstractions)
- [ ] Implement service method in `MigrationService` (Services)
- [ ] Add `Create*Command()` factory method to `CommandLineConfiguration` (Core)
- [ ] Add `Setup*Handler()` method to `CommandLineConfiguration` (Core)
- [ ] Register command in `CommandLineConfiguration` constructor (Core)
- [ ] Add switch case in `RayMigratorService.DoWorkAsync()` (Pipeline)
- [ ] Add `Execute*Async()` method to `RayMigratorService` (Pipeline)
- [ ] Write unit tests
- [ ] Update CLI documentation

## Related Documentation

- [Command Structure](../05-console-layer/command-structure.md)
- [RayMigrator Service](../05-console-layer/raymigrator-service.md)
- [Migration Service](../04-service-layer/migration-service.md)
