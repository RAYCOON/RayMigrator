using AwesomeAssertions;
using Raycoon.RayMigrator.Database.Common;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1: Tests for TransientErrorCodesFile, the parser and loader of the editable TransientErrorCodes.txt that
/// replaces a DAL's built-in transient error codes (ADR-022).
/// </summary>
public class TransientErrorCodesFileTests
{
    [Fact]
    public void Parse_CodesWithCommentsAndBlankLines_ReturnsTrimmedCodesInOrder()
    {
        // Arrange
        string[] lines =
        [
            "# RayMigrator transient error codes for SqlServer.",
            "",
            "-2      # Timeout expired",
            "  233   # Connection closed during initialization",
            "4021",
            "   ",
            "57P01 # alphanumeric SQLSTATE"
        ];

        // Act
        var codes = TransientErrorCodesFile.Parse(lines);

        // Assert
        codes.Should().Equal("-2", "233", "4021", "57P01");
    }

    [Fact]
    public void Parse_OnlyCommentsAndBlankLines_ReturnsEmptyList()
    {
        // Act
        var codes = TransientErrorCodesFile.Parse(["# nothing", "", "   # still nothing"]);

        // Assert
        codes.Should().BeEmpty(because: "an existing file without codes means that nothing is retried");
    }

    [Fact]
    public void Parse_CodeWithWhitespace_ThrowsFormatExceptionNamingTheLine()
    {
        // Act
        Action act = () => TransientErrorCodesFile.Parse(["# header", "-2", "4021 x   # a stray token"]);

        // Assert
        act.Should().Throw<FormatException>().WithMessage("Line 3:*").Which.Message.Should().Contain("[4021 x]");
    }

    [Fact]
    public void GetPath_CombinesBaseDirectoryDataAccessLayersTypeAndFileName()
    {
        // Act
        string path = TransientErrorCodesFile.GetPath("/app", "SqlServer");

        // Assert
        path.Should().Be(Path.Combine("/app", "DataAccessLayers", "SqlServer", "TransientErrorCodes.txt"));
    }

    [Fact]
    public void TryLoad_FileMissing_ReturnsFalseWithProbedPathAndNoCodes()
    {
        // Arrange
        using var temp = new TempDirectory();

        // Act
        bool loaded = TransientErrorCodesFile.TryLoad(temp.Path, "SqlServer", out string path, out var codes);

        // Assert
        loaded.Should().BeFalse(because: "a missing file keeps the built-in list");
        path.Should().Be(TransientErrorCodesFile.GetPath(temp.Path, "SqlServer"));
        codes.Should().BeEmpty();
    }

    [Fact]
    public void TryLoad_FileExists_ReturnsTrueWithParsedCodes()
    {
        // Arrange
        using var temp = new TempDirectory();
        string path = temp.WriteFile("SqlServer", "# codes", "-2 # timeout", "4021");

        // Act
        bool loaded = TransientErrorCodesFile.TryLoad(temp.Path, "SqlServer", out string loadedPath, out var codes);

        // Assert
        loaded.Should().BeTrue();
        loadedPath.Should().Be(path);
        codes.Should().Equal("-2", "4021");
    }

    [Fact]
    public void TryLoad_MalformedFile_ThrowsFormatException()
    {
        // Arrange
        using var temp = new TempDirectory();
        temp.WriteFile("SqlServer", "4021 x");

        // Act
        Action act = () => TransientErrorCodesFile.TryLoad(temp.Path, "SqlServer", out _, out _);

        // Assert
        act.Should().Throw<FormatException>().WithMessage("Line 1:*");
    }

    /// <summary>A throw-away base directory with a DataAccessLayers/{Type}/ layout, deleted on dispose.</summary>
    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RayMigrator_TransientErrorCodesFileTests", Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public string WriteFile(string databaseType, params string[] lines)
        {
            string path = TransientErrorCodesFile.GetPath(Path, databaseType);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, lines);
            return path;
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* ignored: best-effort clean-up of a temp folder */ }
        }
    }
}
