using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Globalization;
using Raycoon.RayMigrator.Core.Configuration.Enums;

namespace Raycoon.RayMigrator.Core.Configuration.Options;

public class CommandLineConfiguration
{
    public RootCommand RootCommand { get; }
    public RayMigratorConsoleOptions? ParsedOptions { get; private set; }

    /// <summary>
    /// Creates the command-line configuration with proper command structure as per specifications
    /// </summary>
    /// <param name="assemblyInfo">Assembly information to display in help</param>
    public CommandLineConfiguration(string assemblyInfo)
    {
        // Create root command with no description to avoid "Description:" header in help output
        RootCommand = new RootCommand();

        // Add global options that apply to all commands
        var showInfoOption = new Option<bool>("--startup-info", "-si")
        {
            Description = "Show startup information",
            DefaultValueFactory = _ => true,
            Recursive = true
        };

        var revealSensitiveDataOption = new Option<bool>("--reveal-sensitive-data", "-rsd")
        {
            Description = "Include sensitive data in logs (WARNING: includes passwords)",
            DefaultValueFactory = _ => false,
            Recursive = true
        };

        var configDirOption = new Option<string?>("--config-dir", "-cd")
        {
            Description = "Override directory where RayMigrator searches for appsettings.json files (default: current directory)",
            Recursive = true
        };

        // Add global options to root
        RootCommand.Options.Add(showInfoOption);
        RootCommand.Options.Add(revealSensitiveDataOption);
        RootCommand.Options.Add(configDirOption);

        // Create and add commands
        var migrateUpCommand = CreateMigrateUpCommand();
        var migrateDownCommand = CreateMigrateDownCommand();
        var validateHashCommand = CreateValidateHashCommand();
        var updateHashCommand = CreateUpdateHashCommand();
        var infoCommand = CreateInfoCommand();
        var baselineCommand = CreateBaselineCommand();
        var fixIssuesCommand = CreateFixIssuesCommand();

        RootCommand.Subcommands.Add(migrateUpCommand);
        RootCommand.Subcommands.Add(migrateDownCommand);
        RootCommand.Subcommands.Add(validateHashCommand);
        RootCommand.Subcommands.Add(updateHashCommand);
        RootCommand.Subcommands.Add(infoCommand);
        RootCommand.Subcommands.Add(baselineCommand);
        RootCommand.Subcommands.Add(fixIssuesCommand);

        // Set up handlers for each command
        SetupMigrateUpHandler(migrateUpCommand, showInfoOption, revealSensitiveDataOption, configDirOption);
        SetupMigrateDownHandler(migrateDownCommand, showInfoOption, revealSensitiveDataOption, configDirOption);
        SetupValidateHashHandler(validateHashCommand, showInfoOption, revealSensitiveDataOption, configDirOption);
        SetupUpdateHashHandler(updateHashCommand, showInfoOption, revealSensitiveDataOption, configDirOption);
        SetupInfoHandler(infoCommand, showInfoOption, revealSensitiveDataOption, configDirOption);
        SetupBaselineHandler(baselineCommand, showInfoOption, revealSensitiveDataOption, configDirOption);
        SetupFixIssuesHandler(fixIssuesCommand, showInfoOption, revealSensitiveDataOption, configDirOption);

        ConfigureHelpLayout(assemblyInfo);
    }

    private void ConfigureHelpLayout(string assemblyInfo)
    {
        for (int i = 0; i < RootCommand.Options.Count; i++)
        {
            if (RootCommand.Options[i] is HelpOption helpOption)
            {
                helpOption.Action = new LogoHelpAction((HelpAction)helpOption.Action!, assemblyInfo);
                break;
            }
        }
    }

    private Command CreateMigrateUpCommand()
    {
        var command = new Command("migrate-up", "Apply pending migrations forward");

        // Required parameters
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

        // Optional parameters
        var runModeOption = new Option<string>("--run-mode", "-rm")
        {
            Description = "Execution mode (migrate, simulate, or validate)",
            DefaultValueFactory = _ => "migrate"
        };

        runModeOption.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value != null)
            {
                var normalizedValue = ResolveEnvironmentVariable(value).ToLowerInvariant();
                if (normalizedValue != "migrate" && normalizedValue != "simulate" && normalizedValue != "validate")
                {
                    result.AddError($"Invalid value for --run-mode: {value}. Valid values are: migrate, simulate, validate.");
                }
            }
        });

        var toReleaseOption = new Option<string?>("--to-release", "-tr")
        {
            Description = "Target release version",
            Required = false
        };

        var allowOutOfOrderOption = new Option<bool>("--allow-out-of-order", "-ooo")
        {
            Description = "Allow out-of-order migration execution",
            DefaultValueFactory = _ => false
        };

        var targetGroupOption = new Option<string[]>("--target-group", "-tg")
        {
            Description = "Filter execution to specific target groups (can be specified multiple times)",
            Arity = ArgumentArity.ZeroOrMore
        };

        var targetGroupMigrationOrderOption = new Option<string?>("--target-group-migration-order", "-tgmo")
        {
            Description = "Explicit TargetGroup migration order (comma-separated aliases, e.g. \"Frontend,Backend\")",
            Arity = ArgumentArity.ZeroOrOne
        };

        var stopRollbackOnMissingRollbackFileOption = new Option<bool?>("--stop-rollback-on-missing-rollback-file", "-sromrf")
        {
            Description = "Stop error-recovery rollback chain when rollback file is missing (default: true)",
            Arity = ArgumentArity.ZeroOrOne
        };

        command.Options.Add(productOption);
        command.Options.Add(environmentOption);
        command.Options.Add(runModeOption);
        command.Options.Add(toReleaseOption);
        command.Options.Add(allowOutOfOrderOption);
        command.Options.Add(targetGroupOption);
        command.Options.Add(targetGroupMigrationOrderOption);
        command.Options.Add(stopRollbackOnMissingRollbackFileOption);

        return command;
    }

    private Command CreateMigrateDownCommand()
    {
        var command = new Command("migrate-down", "Rollback to previous version");

        // Required parameters
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

        var toReleaseOption = new Option<string>("--to-release", "-tr")
        {
            Description = "Target release version",
            Required = true
        };

        // Optional parameters
        var runModeOption = new Option<string>("--run-mode", "-rm")
        {
            Description = "Execution mode (migrate, simulate, or validate)",
            DefaultValueFactory = _ => "migrate"
        };

        runModeOption.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value != null)
            {
                var normalizedValue = ResolveEnvironmentVariable(value).ToLowerInvariant();
                if (normalizedValue != "migrate" && normalizedValue != "simulate" && normalizedValue != "validate")
                {
                    result.AddError($"Invalid value for --run-mode: {value}. Valid values are: migrate, simulate, validate.");
                }
            }
        });

        var targetGroupOption = new Option<string[]>("--target-group", "-tg")
        {
            Description = "Filter execution to specific target groups (can be specified multiple times)",
            Arity = ArgumentArity.ZeroOrMore
        };

        command.Options.Add(productOption);
        command.Options.Add(environmentOption);
        command.Options.Add(runModeOption);
        command.Options.Add(toReleaseOption);
        command.Options.Add(targetGroupOption);

        return command;
    }

    private Command CreateValidateHashCommand()
    {
        var command = new Command("validate-hash", "Verify migration file integrity");

        // Required parameters
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

        // Optional parameters
        var scopeOption = new Option<string>("--scope", "-s")
        {
            Description = "Hash validation scope override (file, sqlblock, or disabled). If omitted, uses per-TargetGroup config."
        };

        scopeOption.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value != null)
            {
                var normalizedValue = ResolveEnvironmentVariable(value).ToLowerInvariant();
                if (normalizedValue != "file" && normalizedValue != "sqlblock" && normalizedValue != "sqlblocks" && normalizedValue != "disabled")
                {
                    result.AddError($"Invalid value for --scope: {value}. Valid values are: file, sqlblock, disabled.");
                }
            }
        });

        var targetGroupOption = new Option<string[]>("--target-group", "-tg")
        {
            Description = "Filter execution to specific target groups (can be specified multiple times)",
            Arity = ArgumentArity.ZeroOrMore
        };

        command.Options.Add(productOption);
        command.Options.Add(environmentOption);
        command.Options.Add(scopeOption);
        command.Options.Add(targetGroupOption);

        return command;
    }

    private Command CreateUpdateHashCommand()
    {
        var command = new Command("update-hash", "Update repository hashes after approved changes");

        // Required parameters
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

    private Command CreateInfoCommand()
    {
        var command = new Command("info", "Display migration status information");

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

        command.Options.Add(productOption);
        command.Options.Add(environmentOption);

        return command;
    }

    private Command CreateBaselineCommand()
    {
        var command = new Command("baseline", "Mark existing database as migrated (all releases, or up to a specific release)");

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

        var toReleaseOption = new Option<string?>("--to-release", "-tr")
        {
            Description = "Target release version to baseline (omit to baseline all releases)",
            Required = false
        };

        var targetGroupOption = new Option<string[]>("--target-group", "-tg")
        {
            Description = "Filter execution to specific target groups (can be specified multiple times)",
            Arity = ArgumentArity.ZeroOrMore
        };

        var targetGroupMigrationOrderOption = new Option<string?>("--target-group-migration-order", "-tgmo")
        {
            Description = "Explicit TargetGroup migration order (comma-separated aliases, e.g. \"Frontend,Backend\")",
            Arity = ArgumentArity.ZeroOrOne
        };

        command.Options.Add(productOption);
        command.Options.Add(environmentOption);
        command.Options.Add(toReleaseOption);
        command.Options.Add(targetGroupOption);
        command.Options.Add(targetGroupMigrationOrderOption);

        return command;
    }

    private Command CreateFixIssuesCommand()
    {
        var command = new Command("fix", "Fix repository inconsistencies (orphaned runs)");

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

        var scopeOption = new Option<string>("--scope", "-s")
        {
            Description = "Fix scope (orphanedruns, all)",
            DefaultValueFactory = _ => "OrphanedRuns"
        };

        scopeOption.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value != null)
            {
                var normalizedValue = ResolveEnvironmentVariable(value).ToLowerInvariant();
                if (normalizedValue != "all" && normalizedValue != "orphanedruns")
                {
                    result.AddError($"Invalid value for --scope: {value}. Valid values are: orphanedruns, all.");
                }
            }
        });

        var olderThanOption = new Option<int>("--older-than", "-ot")
        {
            Description = "Only fix runs older than N minutes (0 = immediate)",
            DefaultValueFactory = _ => 60
        };

        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Only show what would be fixed without applying changes",
            DefaultValueFactory = _ => false
        };

        var lastMigrationStatusOption = new Option<string>("--last-migration-status", "-lms")
        {
            Description = "Status for orphaned migrations: not-migrated (re-execute next time) or migrated (skip next time)",
            DefaultValueFactory = _ => "not-migrated"
        };

        lastMigrationStatusOption.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value != null)
            {
                var normalizedValue = ResolveEnvironmentVariable(value).ToLowerInvariant();
                if (normalizedValue != "migrated" && normalizedValue != "not-migrated")
                {
                    result.AddError($"Invalid value for --last-migration-status: {value}. Valid values are: migrated, not-migrated.");
                }
            }
        });

        command.Options.Add(productOption);
        command.Options.Add(environmentOption);
        command.Options.Add(scopeOption);
        command.Options.Add(olderThanOption);
        command.Options.Add(dryRunOption);
        command.Options.Add(lastMigrationStatusOption);

        return command;
    }

    private void SetupMigrateUpHandler(Command command, Option<bool> showInfoOption, Option<bool> revealSensitiveDataOption, Option<string?> configDirOption)
    {
        command.SetAction(parseResult =>
        {
            var runModeString = parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--run-mode")) ?? "Migrate";
            var runMode = ParseRunMode(runModeString);

            var allowOutOfOrder = parseResult.GetValue(command.Options.OfType<Option<bool>>().First(o => o.Name == "--allow-out-of-order"));

            var tgeoRaw = parseResult.GetValue(command.Options.OfType<Option<string?>>().First(o => o.Name == "--target-group-migration-order"));
            var tgeoArray = ParseCommaSeparatedToArray(tgeoRaw);

            var stopRollbackOnMissingRollbackFile = parseResult.GetValue(
                command.Options.OfType<Option<bool?>>().First(o => o.Name == "--stop-rollback-on-missing-rollback-file"));

            ParsedOptions = new RayMigratorConsoleOptions
            {
                Command = MigrationCommand.MigrateUp,
                Product = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--product"))!),
                Environment = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--environment"))!),
                RunMode = runMode,
                TargetReleaseVersion = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string?>>().First(o => o.Name == "--to-release")) ?? ""),
                ShowStartupInfo = parseResult.GetValue(showInfoOption),
                RevealSensitiveData = parseResult.GetValue(revealSensitiveDataOption),
                HashValidationScope = null,
                AllowOutOfOrder = allowOutOfOrder,
                StopRollbackOnMissingRollbackFile = stopRollbackOnMissingRollbackFile,
                TargetGroupAliases = parseResult.GetValue(command.Options.OfType<Option<string[]>>()
                    .First(o => o.Name == "--target-group"))
                    ?.Select(a => ResolveEnvironmentVariable(a))
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .ToArray(),
                TargetGroupMigrationOrder = tgeoArray,
                ConfigDir = ResolveConfigDir(parseResult.GetValue(configDirOption)),
            };
        });
    }

    private void SetupMigrateDownHandler(Command command, Option<bool> showInfoOption, Option<bool> revealSensitiveDataOption, Option<string?> configDirOption)
    {
        command.SetAction(parseResult =>
        {
            var runModeString = parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--run-mode")) ?? "Migrate";
            var runMode = ParseRunMode(runModeString);

            ParsedOptions = new RayMigratorConsoleOptions
            {
                Command = MigrationCommand.MigrateDown,
                Product = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--product"))!),
                Environment = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--environment"))!),
                RunMode = runMode,
                TargetReleaseVersion = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--to-release"))!),
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

    private void SetupValidateHashHandler(Command command, Option<bool> showInfoOption, Option<bool> revealSensitiveDataOption, Option<string?> configDirOption)
    {
        command.SetAction(parseResult =>
        {
            var scopeString = parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--scope"));
            HashValidationScope? scope = scopeString != null ? ParseHashValidationScope(scopeString) : null;

            ParsedOptions = new RayMigratorConsoleOptions
            {
                Command = MigrationCommand.ValidateHash,
                Product = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--product"))!),
                Environment = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--environment"))!),
                RunMode = MigrationRunMode.Validate,
                TargetReleaseVersion = null,
                ShowStartupInfo = parseResult.GetValue(showInfoOption),
                RevealSensitiveData = parseResult.GetValue(revealSensitiveDataOption),
                HashValidationScope = scope,
                TargetGroupAliases = parseResult.GetValue(command.Options.OfType<Option<string[]>>()
                    .First(o => o.Name == "--target-group"))
                    ?.Select(a => ResolveEnvironmentVariable(a))
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .ToArray(),
                ConfigDir = ResolveConfigDir(parseResult.GetValue(configDirOption)),
            };
        });
    }

    private void SetupUpdateHashHandler(Command command, Option<bool> showInfoOption, Option<bool> revealSensitiveDataOption, Option<string?> configDirOption)
    {
        command.SetAction(parseResult =>
        {
            ParsedOptions = new RayMigratorConsoleOptions
            {
                Command = MigrationCommand.UpdateHash,
                Product = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--product"))!),
                Environment = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--environment"))!),
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

    private void SetupInfoHandler(Command command, Option<bool> showInfoOption, Option<bool> revealSensitiveDataOption, Option<string?> configDirOption)
    {
        command.SetAction(parseResult =>
        {
            ParsedOptions = new RayMigratorConsoleOptions
            {
                Command = MigrationCommand.Info,
                Product = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--product"))!),
                Environment = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--environment"))!),
                RunMode = MigrationRunMode.Migrate,
                TargetReleaseVersion = null,
                ShowStartupInfo = parseResult.GetValue(showInfoOption),
                RevealSensitiveData = parseResult.GetValue(revealSensitiveDataOption),
                HashValidationScope = null,
                ConfigDir = ResolveConfigDir(parseResult.GetValue(configDirOption)),
            };
        });
    }

    private void SetupBaselineHandler(Command command, Option<bool> showInfoOption, Option<bool> revealSensitiveDataOption, Option<string?> configDirOption)
    {
        command.SetAction(parseResult =>
        {
            var tgeoRaw = parseResult.GetValue(command.Options.OfType<Option<string?>>().First(o => o.Name == "--target-group-migration-order"));
            var tgeoArray = ParseCommaSeparatedToArray(tgeoRaw);

            ParsedOptions = new RayMigratorConsoleOptions
            {
                Command = MigrationCommand.Baseline,
                Product = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--product"))!),
                Environment = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--environment"))!),
                RunMode = MigrationRunMode.Migrate,
                TargetReleaseVersion = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string?>>().First(o => o.Name == "--to-release")) ?? ""),
                ShowStartupInfo = parseResult.GetValue(showInfoOption),
                RevealSensitiveData = parseResult.GetValue(revealSensitiveDataOption),
                HashValidationScope = null,
                TargetGroupAliases = parseResult.GetValue(command.Options.OfType<Option<string[]>>()
                    .First(o => o.Name == "--target-group"))
                    ?.Select(a => ResolveEnvironmentVariable(a))
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .ToArray(),
                TargetGroupMigrationOrder = tgeoArray,
                ConfigDir = ResolveConfigDir(parseResult.GetValue(configDirOption)),
            };
        });
    }

    private void SetupFixIssuesHandler(Command command, Option<bool> showInfoOption, Option<bool> revealSensitiveDataOption, Option<string?> configDirOption)
    {
        command.SetAction(parseResult =>
        {
            var scopeString = parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--scope")) ?? "OrphanedRuns";
            var scope = ParseFixIssuesScope(scopeString);

            var olderThan = parseResult.GetValue(command.Options.OfType<Option<int>>().First(o => o.Name == "--older-than"));
            var dryRun = parseResult.GetValue(command.Options.OfType<Option<bool>>().First(o => o.Name == "--dry-run"));
            var lastMigrationStatusString = parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--last-migration-status")) ?? "not-migrated";
            var lastMigrationStatus = ParseLastMigrationStatus(lastMigrationStatusString);

            ParsedOptions = new RayMigratorConsoleOptions
            {
                Command = MigrationCommand.FixIssues,
                Product = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--product"))!),
                Environment = ResolveEnvironmentVariable(parseResult.GetValue(command.Options.OfType<Option<string>>().First(o => o.Name == "--environment"))!),
                RunMode = MigrationRunMode.Migrate,
                TargetReleaseVersion = null,
                ShowStartupInfo = parseResult.GetValue(showInfoOption),
                RevealSensitiveData = parseResult.GetValue(revealSensitiveDataOption),
                HashValidationScope = null,
                FixIssues = scope,
                FixOlderThanMinutes = olderThan,
                FixDryRun = dryRun,
                FixAssumedMigrationStatus = lastMigrationStatus,
                ConfigDir = ResolveConfigDir(parseResult.GetValue(configDirOption)),
            };
        });
    }

    /// <summary>
    /// Parses a string to MigrationRunMode enum
    /// </summary>
    private static MigrationRunMode ParseRunMode(string value)
    {
        var normalizedValue = ResolveEnvironmentVariable(value).ToLowerInvariant();
        return normalizedValue switch
        {
            "migrate" => MigrationRunMode.Migrate,
            "simulate" => MigrationRunMode.Simulate,
            "validate" => MigrationRunMode.Validate,
            _ => MigrationRunMode.Migrate
        };
    }

    /// <summary>
    /// Parses a string to HashValidationScope enum
    /// </summary>
    private static HashValidationScope ParseHashValidationScope(string value)
    {
        var normalizedValue = ResolveEnvironmentVariable(value).ToLowerInvariant();
        return normalizedValue switch
        {
            "file" => HashValidationScope.File,
            "sqlblock" => HashValidationScope.SqlBlocks,
            "sqlblocks" => HashValidationScope.SqlBlocks,
            "disabled" => HashValidationScope.Disabled,
            _ => HashValidationScope.File
        };
    }

    /// <summary>
    /// Parses a string to FixIssues enum
    /// </summary>
    private static FixIssues ParseFixIssuesScope(string value)
    {
        var normalizedValue = ResolveEnvironmentVariable(value).ToLowerInvariant();
        return normalizedValue switch
        {
            "all" => FixIssues.All,
            "orphanedruns" => FixIssues.OrphanedRuns,
            _ => FixIssues.OrphanedRuns
        };
    }

    /// <summary>
    /// Parses a string to MigrationStatus for the --last-migration-status option
    /// </summary>
    private static MigrationStatus ParseLastMigrationStatus(string value)
    {
        var normalizedValue = ResolveEnvironmentVariable(value).ToLowerInvariant();
        return normalizedValue switch
        {
            "migrated" => MigrationStatus.Migrated,
            "not-migrated" => MigrationStatus.NotMigrated,
            _ => MigrationStatus.NotMigrated
        };
    }

    /// <summary>
    /// Parses a comma-separated string into a trimmed, non-empty string array. Returns null if input is null/whitespace.
    /// </summary>
    internal static string[]? ParseCommaSeparatedToArray(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var result = raw.Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();

        return result.Length > 0 ? result : null;
    }

    /// <summary>
    /// Resolves the --config-dir value: applies ENV variable replacement and converts to absolute path.
    /// Returns null if the raw value is null or whitespace.
    /// </summary>
    private static string? ResolveConfigDir(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var resolved = ResolveEnvironmentVariable(raw);
        return string.IsNullOrWhiteSpace(resolved) ? null : Path.GetFullPath(resolved);
    }

    /// <summary>
    /// Resolves environment variables in the format {ENV:VARIABLE_NAME}
    /// </summary>
    private static string ResolveEnvironmentVariable(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (value.StartsWith("{ENV:") && value.EndsWith("}"))
        {
            var envVar = value[5..^1];
            var envValue = Environment.GetEnvironmentVariable(envVar);

            if (string.IsNullOrWhiteSpace(envValue))
            {
                throw new ArgumentException($"Environment variable '{envVar}' is not set.");
            }

            return envValue;
        }

        return value;
    }
}

/// <summary>
/// Extension methods for Option configuration
/// </summary>
public static class OptionExtensions
{
    /// <summary>
    /// Extends an Option to support environment variables in the format {ENV:VAR_NAME}
    /// </summary>
    public static Option<T> FromEnvironment<T>(this Option<T> option) where T : IParsable<T>
    {
        return new Option<T>(option.Name, option.Aliases.ToArray())
        {
            Description = option.Description,
            Required = option.Required,
            HelpName = option.HelpName,
            DefaultValueFactory = option.DefaultValueFactory,
            AllowMultipleArgumentsPerToken = option.AllowMultipleArgumentsPerToken,
            Arity = option.Arity
        }.WithEnvironmentVariableSupport();
    }

    /// <summary>
    /// Replaces {ENV:VAR_NAME} in arguments with the environment variable value
    /// </summary>
    private static Option<T> WithEnvironmentVariableSupport<T>(this Option<T> option) where T : IParsable<T>
    {
        option.Validators.Add(result =>
        {
            var value = result.Tokens.FirstOrDefault()?.Value;
            if (!string.IsNullOrWhiteSpace(value) && value.StartsWith("{ENV:") && value.EndsWith("}"))
            {
                var envVar = value[5..^1];
                var envValue = Environment.GetEnvironmentVariable(envVar);

                if (string.IsNullOrWhiteSpace(envValue))
                {
                    result.AddError($"Environment variable '{envVar}' is not set.");
                }
            }
        });

        return option;
    }

    /// <summary>
    /// Resolves {ENV:VAR_NAME} and returns the environment variable value
    /// </summary>
    public static T ResolveEnvironmentVariable<T>(T value) where T : IParsable<T>
    {
        if (value is string strValue && !string.IsNullOrWhiteSpace(strValue) && strValue.StartsWith("{ENV:") && strValue.EndsWith("}"))
        {
            var envVar = strValue[5..^1];
            var envValue = Environment.GetEnvironmentVariable(envVar);

            if (!string.IsNullOrWhiteSpace(envValue))
            {
                try
                {
                    return T.Parse(envValue, CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    throw new ArgumentException($"Invalid value '{envValue}' for option.");
                }
            }
            throw new ArgumentException($"Environment variable '{envVar}' is not set.");
        }
        return value;
    }
}

internal class LogoHelpAction : SynchronousCommandLineAction
{
    private readonly HelpAction _defaultHelp;
    private readonly string _assemblyInfo;

    public LogoHelpAction(HelpAction defaultHelp, string assemblyInfo)
    {
        _defaultHelp = defaultHelp;
        _assemblyInfo = assemblyInfo;
    }

    public override int Invoke(ParseResult parseResult)
    {
        var output = Console.Out;

        if (parseResult.CommandResult.Command is RootCommand)
        {
            // Capture default help output, strip empty "Description:" section, prepend logo
            using var buffer = new System.IO.StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(buffer);

            _defaultHelp.Invoke(parseResult);

            Console.SetOut(originalOut);

            string helpText = buffer.ToString();

            // Remove the empty "Description:" section (header + blank line)
            helpText = System.Text.RegularExpressions.Regex.Replace(
                helpText,
                @"^Description:\s*\r?\n\s*\r?\n",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.Multiline);

            output.Write(_assemblyInfo);
            output.WriteLine();
            output.WriteLine();
            output.Write(helpText);
        }
        else
        {
            _defaultHelp.Invoke(parseResult);
        }

        return 0;
    }

    public override bool ClearsParseErrors => true;
}
