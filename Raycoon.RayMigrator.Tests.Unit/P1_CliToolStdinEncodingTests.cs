using System.Reflection;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Raycoon.RayMigrator.Core;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Models;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// #4: in Stdin mode ExecuteWithCliTool reads the file with the product's MigrationFilesEncoding (the same
/// decoding the hash was computed from) and strictly; invalid bytes abort before the tool is started.
/// </summary>
public class CliToolStdinEncodingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "raymigrator-clienc-" + Guid.NewGuid().ToString("N"));

    public CliToolStdinEncodingTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

    private static (MigrationService Service, ICliToolExecutor Executor) CreateService(string productEncoding)
    {
        var service = TestFactories.CreateUninitializedMigrationService();
        var executor = Substitute.For<ICliToolExecutor>();

        var options = new RayMigratorOptions
        {
            Products = new List<ProductOptions>
            {
                new ProductOptions(null) { Alias = "P", MigrationFilesRootDirectory = "/tmp", MigrationFilesEncoding = productEncoding }
            }
        };
        Set(service, "_options", Options.Create(options));
        Set(service, "_cliToolExecutor", executor);

        var accessor = (IMigrationContextAccessor)Get(service, "_ctxAccessor");
        accessor.Current.RayMigratorConsoleOptions = new RayMigratorConsoleOptions
        {
            Command = MigrationCommand.MigrateUp, Product = "P", Environment = "Dev", RunMode = MigrationRunMode.Migrate,
            ShowStartupInfo = false, RevealSensitiveData = false
        };

        return (service, executor);
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

    private static object Get(object target, string field) =>
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;

    private static CliToolOptions StdinTool() => new()
    {
        Alias = "psql", ExecutablePath = "psql", ArgumentTemplate = "-q", InputMode = "Stdin"
    };

    [Fact]
    public async Task ExecuteWithCliTool_StdinMode_InvalidBytesForProductEncoding_ThrowsBeforeInvokingTool()
    {
        var (service, executor) = CreateService("ASCII");
        string path = Path.Combine(_dir, "001_umlaut.sql");
        File.WriteAllBytes(path, EncodingSupportBytes("SELECT 'Grüße';"));
        var file = new MigrationFileInfo { Filename = "001_umlaut.sql", FilenameWithRelativePath = "Release 1.0/Backend/001_umlaut.sql", FullPath = path };

        var act = () => service.ExecuteWithCliTool(file, new TargetGroupOptions { Alias = "Backend" },
            new TargetOptions { Alias = "MainDB", ConnectionString = "x" }, 1, MigrationRunMode.Migrate, StdinTool());

        await act.Should().ThrowAsync<MigrationFileParsingException>()
            .WithMessage("*[Release 1.0/Backend/001_umlaut.sql]*")
            .WithMessage("*[ASCII]*")
            .WithMessage("*product [P]*");
        await executor.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
    }

    private static byte[] EncodingSupportBytes(string text) => Raycoon.RayMigrator.Core.Configuration.EncodingSupport.StrictUtf8.GetBytes(text);
}
