using Raycoon.RayMigrator.Core.Extensions;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Sources;
using Raycoon.RayMigrator.Pipeline;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Shared.Exceptions;
using System.Globalization;
using CommandLineConfiguration = Raycoon.RayMigrator.Core.Configuration.Options.CommandLineConfiguration;

namespace Raycoon.RayMigrator;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

        try
        {
            string assemblyInfo = AssemblyInfoHelper.GetAssemblyInfo();

            #region CommandLineParser

            // For installation of dotnet-suggest run: dotnet tool install --global --allow-roll-forward dotnet-suggest
            var commandLineConfiguration = new CommandLineConfiguration(assemblyInfo);

            int parseResult;
            try
            {
                parseResult = await commandLineConfiguration.RootCommand.Parse(args).InvokeAsync();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"ERROR parsing command line arguments:\n{ex.GetExceptionDetails()}");
                Console.ResetColor();
                return 5;
            }

            if (parseResult != 0) return parseResult;

            // If ParsedOptions is null, a help or version request was handled — exit cleanly
            var rayMigratorConsoleOptions = commandLineConfiguration.ParsedOptions;
            if (rayMigratorConsoleOptions is null) return 0;

            SensitiveDataMasker.Initialize(rayMigratorConsoleOptions.RevealSensitiveData);

            #endregion CommandLineParser


            #region Resolve environment

            var (environment, environmentOrigin, envErrorCode) = EnvironmentResolver.Resolve(rayMigratorConsoleOptions, assemblyInfo);
            if (envErrorCode.HasValue) return envErrorCode.Value;

            #endregion Resolve environment


            #region Standalone mode

            return await RunDirectMode(
                new JsonOptionsSource(rayMigratorConsoleOptions.ConfigDir),
                args, rayMigratorConsoleOptions, assemblyInfo, environment!, environmentOrigin!);

            #endregion Standalone mode
        }
        catch (ApplicationStartupException ex)
        {
            Console.WriteLine($"\n*** ERROR (RayMigrator) *** [{DateTime.Now.ToString("u")}]\n{ApplicationStartupException.AbortMessage}: " + ex.GetExceptionDetails());
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n*** ERROR (RayMigrator) *** [{DateTime.Now.ToString("u")}]\n" + ex.GetExceptionDetails());
            return 100;
        }
    }

    /// <summary>
    /// Runs RayMigrator in Direct mode (Standalone).
    /// Loads configuration from JSON, then delegates to the unified pipeline.
    /// </summary>
    private static async Task<int> RunDirectMode(
        IOptionsSource optionsSource,
        string[] args,
        RayMigratorConsoleOptions rayMigratorConsoleOptions,
        string assemblyInfo,
        string environment,
        string environmentOrigin)
    {
        var sourceResult = await optionsSource.LoadAsync(rayMigratorConsoleOptions.Product, environment);

        return await DirectModePipeline.ExecuteAsync(
            args, sourceResult, rayMigratorConsoleOptions, assemblyInfo, environment, environmentOrigin);
    }
}
