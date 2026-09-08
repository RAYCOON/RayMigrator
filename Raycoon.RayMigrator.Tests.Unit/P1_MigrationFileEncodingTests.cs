using System.Text;
using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Services;
using Raycoon.RayMigrator.Shared.Constants;
using Raycoon.RayMigrator.Shared.Exceptions;
using Raycoon.RayMigrator.Tests.Unit.Helpers;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// #4: migration files are read BOM-aware and strictly. A byte-order mark overrides the configured encoding
/// and is stripped; bytes that are invalid for the encoding abort with a MigrationFileParsingException that
/// names the file and the encoding instead of being replaced by U+FFFD or '?'. migsettings.txt is always UTF-8.
/// </summary>
public class MigrationFileEncodingTests : IDisposable
{
    private const string Text = "SELECT 'Grüße – €';";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "raymigrator-enc-" + Guid.NewGuid().ToString("N"));

    public MigrationFileEncodingTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { /* best effort */ } }

    private string WriteBytes(string name, byte[] bytes)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static byte[] WithPreamble(Encoding encoding, string text) =>
        encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();

    private static string Read(string path, string? configuredEncoding) =>
        MigrationService.ReadMigrationText(path, MigrationService.GetFileEncoding(configuredEncoding),
            configuredEncoding ?? "UTF-8", Path.GetFileName(path), "hint");

    public static TheoryData<string, string> BomLayoutsUnderEveryConfiguredEncoding()
    {
        var data = new TheoryData<string, string>();
        foreach (var configured in new[] { "UTF-8", "ASCII", "UTF-16", "windows-1252" })
            foreach (var layout in new[] { "utf8-bom", "utf16le-bom", "utf16be-bom", "utf32le-bom", "utf32be-bom" })
                data.Add(configured, layout);
        return data;
    }

    private static byte[] Layout(string layout) => layout switch
    {
        "utf8-bom" => WithPreamble(new UTF8Encoding(true), Text),
        "utf16le-bom" => WithPreamble(new UnicodeEncoding(false, true), Text),
        "utf16be-bom" => WithPreamble(new UnicodeEncoding(true, true), Text),
        "utf32le-bom" => WithPreamble(new UTF32Encoding(false, true), Text),
        "utf32be-bom" => WithPreamble(new UTF32Encoding(true, true), Text),
        _ => throw new ArgumentOutOfRangeException(nameof(layout))
    };

    [Theory]
    [MemberData(nameof(BomLayoutsUnderEveryConfiguredEncoding))]
    public void ReadMigrationText_FileWithBom_DecodesByTheBomAndStripsIt(string configured, string layout)
    {
        string path = WriteBytes($"{layout}.sql", Layout(layout));

        Read(path, configured).Should().Be(Text, "the BOM wins over the configured encoding and is not part of the text");
    }

    [Fact]
    public void ReadMigrationText_Utf32BomUnderConfiguredUtf16_IsNotMistakenForUtf16()
    {
        // StreamReader takes the FF FE prefix of the UTF-32 LE BOM as a UTF-16 LE BOM when UTF-16 is configured
        string path = WriteBytes("utf32.sql", Layout("utf32le-bom"));

        Read(path, "UTF-16").Should().Be(Text);
    }

    [Theory]
    [InlineData("UTF-8")]
    [InlineData("windows-1252")]
    [InlineData("iso-8859-1")]
    public void ReadMigrationText_NoBom_UsesTheConfiguredEncoding(string configured)
    {
        var encoding = EncodingSupport.GetStrictEncoding(configured);
        string content = configured == "UTF-8" ? Text : "SELECT 'Grüße';"; // no '–'/'€' in Latin-1
        string path = WriteBytes("plain.sql", encoding.GetBytes(content));

        Read(path, configured).Should().Be(content);
    }

    [Fact]
    public void ReadMigrationText_Windows1252BytesUnderUtf8_ThrowsNamingFileEncodingAndBytes()
    {
        string path = WriteBytes("001_umlaut.sql", Encoding.Latin1.GetBytes("CREATE TABLE T (Name TEXT);\nINSERT INTO T (Name) VALUES ('Grüße');\n"));

        var act = () => MigrationService.ReadMigrationText(path, MigrationService.GetFileEncoding("UTF-8"), "UTF-8", "Release 1.0/Backend/001_umlaut.sql", "Check MigrationFilesEncoding.");

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*[Release 1.0/Backend/001_umlaut.sql]*")
            .WithMessage("*[UTF-8]*")
            .WithMessage("*invalid byte sequence [FC*", "the offending bytes are named")
            .WithMessage("*byte offset 60*", "the first line is 28 bytes, 'INSERT INTO T (Name) VALUES ('Gr' is 32 more, the ü (FC) sits at index 60")
            .Which.ErrorCode.Should().Be(TemplateResultCode.MigrationFileParsingFailed);
    }

    [Fact]
    public void ReadMigrationText_UmlautUnderAscii_Throws()
    {
        string path = WriteBytes("ascii.sql", EncodingSupport.StrictUtf8.GetBytes("SELECT 'ä';"));

        var act = () => Read(path, "ASCII");

        act.Should().Throw<MigrationFileParsingException>().WithMessage("*[ASCII]*");
    }

    [Fact]
    public void ReadMigrationText_Utf16WithoutBom_UnderUtf8_Throws()
    {
        string path = WriteBytes("utf16-nobom.sql", new UnicodeEncoding(false, false).GetBytes(Text));

        var act = () => Read(path, "UTF-8");

        act.Should().Throw<MigrationFileParsingException>("UTF-16 without BOM is not valid UTF-8 and must not be decoded silently");
    }

    [Fact]
    public void ReadMigrationText_PureAscii_IsIdenticalUnderEveryEncoding()
    {
        string path = WriteBytes("ascii-only.sql", Encoding.ASCII.GetBytes("SELECT 1;"));

        foreach (var configured in new[] { "UTF-8", "ASCII", "iso-8859-1", "windows-1252" })
            Read(path, configured).Should().Be("SELECT 1;");
    }

    [Fact]
    public void ReadMigrationText_BomAndNoBomVariants_YieldTheSameTextAndHash()
    {
        string withBom = WriteBytes("a-bom.sql", WithPreamble(new UTF8Encoding(true), Text));
        string withoutBom = WriteBytes("a-nobom.sql", EncodingSupport.StrictUtf8.GetBytes(Text));

        Read(withBom, "UTF-8").Should().Be(Read(withoutBom, "UTF-8"),
            "re-saving a file with or without BOM must not change its content hash");
    }

    [Fact]
    public void DetectByteOrderMark_ChecksUtf32BeforeUtf16()
    {
        MigrationService.DetectByteOrderMark(new byte[] { 0xFF, 0xFE, 0x00, 0x00 })!.Value.Label.Should().Be("UTF-32 LE");
        MigrationService.DetectByteOrderMark(new byte[] { 0xFF, 0xFE, 0x41, 0x00 })!.Value.Label.Should().Be("UTF-16 LE");
        MigrationService.DetectByteOrderMark(new byte[] { 0xFE, 0xFF, 0x00, 0x41 })!.Value.Label.Should().Be("UTF-16 BE");
        MigrationService.DetectByteOrderMark(new byte[] { 0x00, 0x00, 0xFE, 0xFF })!.Value.Label.Should().Be("UTF-32 BE");
        MigrationService.DetectByteOrderMark(new byte[] { 0xEF, 0xBB, 0xBF, 0x41 })!.Value.Label.Should().Be("UTF-8");
        MigrationService.DetectByteOrderMark(new byte[] { 0x41, 0x42 }).Should().BeNull();
        MigrationService.DetectByteOrderMark(Array.Empty<byte>()).Should().BeNull();
    }

    #region ReadMigrationText edge cases (#4)

    [Theory]
    [InlineData("UTF-8")]
    [InlineData("UTF-16")]
    [InlineData("windows-1252")]
    public void ReadMigrationText_EmptyFile_ReturnsEmptyString(string configured)
    {
        string path = WriteBytes("empty.sql", Array.Empty<byte>());

        Read(path, configured).Should().BeEmpty();
    }

    [Theory]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF })]
    [InlineData(new byte[] { 0xFF, 0xFE })]
    [InlineData(new byte[] { 0xFE, 0xFF })]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x00, 0x00 })]
    [InlineData(new byte[] { 0x00, 0x00, 0xFE, 0xFF })]
    public void ReadMigrationText_BomOnlyFile_ReturnsEmptyString(byte[] bom)
    {
        string path = WriteBytes("bom-only.sql", bom);

        Read(path, "UTF-8").Should().BeEmpty("a BOM without payload is an empty file, not an error");
    }

    [Fact]
    public void ReadMigrationText_InvalidByteAfterBom_ReportsAbsoluteFileOffsetAndBomLabel()
    {
        // UTF-8 BOM (3 bytes) + "AB" + 0xFC + "C": the decoder's index is relative to the payload, the message must add the BOM length
        string path = WriteBytes("bom-invalid.sql", new byte[] { 0xEF, 0xBB, 0xBF, 0x41, 0x42, 0xFC, 0x43 });

        var act = () => Read(path, "windows-1252");

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*[UTF-8 (byte-order mark)]*", "the BOM decided the encoding, not the configured windows-1252")
            .WithMessage("*[FC]*")
            .WithMessage("*byte offset 5*");
    }

    [Fact]
    public void ReadMigrationText_Utf16OddByteCount_ReportsOffsetBehindTheBom()
    {
        string path = WriteBytes("utf16-odd.sql", new byte[] { 0xFF, 0xFE, 0x41, 0x00, 0x42 });

        var act = () => Read(path, "UTF-8");

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*[UTF-16 LE (byte-order mark)]*")
            .WithMessage("*byte offset 4*", "the dangling byte is the fifth byte of the file");
    }

    [Fact]
    public void ReadMigrationText_TruncatedUtf8SequenceAtEof_ThrowsNamingTheLeadByte()
    {
        string path = WriteBytes("truncated.sql", new byte[] { 0x41, 0xC3 });

        var act = () => Read(path, "UTF-8");

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*[C3]*")
            .WithMessage("*byte offset 1*");
    }

    [Fact]
    public void ReadMigrationText_HighByteUnderAscii_ReportsOffsetZero()
    {
        string path = WriteBytes("high-ascii.sql", new byte[] { 0x80 });

        var act = () => Read(path, "ASCII");

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*[ASCII]*")
            .WithMessage("*[80]*")
            .WithMessage("*byte offset 0*");
    }

    [Fact]
    public void ReadMigrationText_Utf16LeWithoutBom_UnderConfiguredUtf16_Decodes()
    {
        string path = WriteBytes("utf16-nobom.sql", new UnicodeEncoding(false, false).GetBytes(Text));

        Read(path, "UTF-16").Should().Be(Text);
    }

    [Fact]
    public void ReadMigrationText_Utf32WithoutBom_UnderConfiguredUtf32_Decodes()
    {
        string path = WriteBytes("utf32-nobom.sql", new UTF32Encoding(false, false).GetBytes(Text));

        Read(path, "UTF-32").Should().Be(Text);
    }

    [Fact]
    public void ReadMigrationText_Utf16BeWithoutBom_UnderConfiguredUtf16_DecodesAsLittleEndianWithoutError()
    {
        // Inherent limitation: byte-swapped UTF-16 consists of valid code units, so strictness cannot catch
        // the wrong endianness. Documented here so nobody expects an exception; a BOM resolves it.
        string path = WriteBytes("utf16-be-nobom.sql", new UnicodeEncoding(true, false).GetBytes("AB"));

        Read(path, "UTF-16").Should().Be("䄀䈀");
    }

    #endregion

    #region migsettings.txt is always UTF-8

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParseMigSettingsFile_Utf8WithOrWithoutBom_KeepsTheUmlaut(bool bom)
    {
        var encoding = new UTF8Encoding(bom);
        string path = WriteBytes("migsettings.txt", WithPreamble(encoding, "[RayMigrator]\nEnvironments = [\"Grüße\"]\n"));
        var service = TestFactories.CreateUninitializedMigrationService();

        var entry = service.ParseMigSettingsFile(path);

        entry.Environments.Should().Equal("Grüße");
    }

    [Fact]
    public void ParseMigSettingsFile_Windows1252_ThrowsAndPointsToUtf8()
    {
        string path = WriteBytes("migsettings.txt", Encoding.Latin1.GetBytes("[RayMigrator]\nEnvironments = [\"Grüße\"]\n"));
        var service = TestFactories.CreateUninitializedMigrationService();

        var act = () => service.ParseMigSettingsFile(path);

        act.Should().Throw<MigrationFileParsingException>()
            .WithMessage("*[UTF-8]*")
            .WithMessage("*migsettings.txt files must be saved as UTF-8*")
            .Which.Message.Should().NotContain("MigrationFilesEncoding for product", "that option does not apply to settings files");
    }

    #endregion

    #region CLI tool Stdin mode pipes UTF-8 without BOM

    [Fact]
    public void CreateStartInfo_StdinMode_UsesUtf8WithoutBomRegardlessOfConsoleCodePage()
    {
        var psi = CliToolExecutor.CreateStartInfo(new CliToolExecutionRequest
        {
            ExecutablePath = "psql", Arguments = "", InputMode = CliToolInputMode.Stdin, FileContent = "SELECT 'Grüße';",
            FilePath = "x.sql", Filename = "x.sql", TimeoutInSeconds = 30, ExitCodeMatcher = ExitCodeMatcher.Default
        });

        psi.RedirectStandardInput.Should().BeTrue();
        psi.StandardInputEncoding.Should().NotBeNull("without it .NET uses Console.InputEncoding, cp850 on a classic Windows console");
        psi.StandardInputEncoding!.WebName.Should().Be("utf-8");
        psi.StandardInputEncoding.GetPreamble().Should().BeEmpty("a BOM would reach the tool as the first bytes of the SQL");
    }

    [Fact]
    public void CreateStartInfo_FileMode_DoesNotRedirectStdin()
    {
        var psi = CliToolExecutor.CreateStartInfo(new CliToolExecutionRequest
        {
            ExecutablePath = "sqlcmd", Arguments = "-i x.sql", InputMode = CliToolInputMode.File,
            FilePath = "x.sql", Filename = "x.sql", TimeoutInSeconds = 30, ExitCodeMatcher = ExitCodeMatcher.Default
        });

        psi.RedirectStandardInput.Should().BeFalse();
        psi.StandardInputEncoding.Should().BeNull();
    }

    #endregion
}
