using System.Text;
using AwesomeAssertions;
using Raycoon.RayMigrator.Core.Configuration;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// #4: EncodingSupport is the single place that turns a configured encoding name into a strict Encoding.
/// Its decoder AND encoder must throw instead of replacing, UTF-8 must never emit a BOM, and code-page
/// names must resolve without the caller registering anything.
/// </summary>
public class EncodingSupportTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetStrictEncoding_NullOrWhitespace_ReturnsStrictUtf8Instance(string? name)
    {
        EncodingSupport.GetStrictEncoding(name).Should().BeSameAs(EncodingSupport.StrictUtf8);
    }

    [Fact]
    public void StrictUtf8_HasNoPreambleAndThrowsOnInvalidBytes()
    {
        EncodingSupport.StrictUtf8.GetPreamble().Should().BeEmpty("it is used as the BOM-stripped decoder and for piping to CLI tools");

        var act = () => EncodingSupport.StrictUtf8.GetString(new byte[] { 0x41, 0xFC });

        act.Should().Throw<DecoderFallbackException>();
    }

    [Theory]
    [InlineData("windows-1252")]
    [InlineData("cp1252")]
    [InlineData("iso-8859-1")]
    [InlineData("UTF-16")]
    [InlineData("UTF-32")]
    [InlineData("ASCII")]
    public void GetStrictEncoding_KnownName_DecoderThrowsOnInvalidInput(string name)
    {
        var encoding = EncodingSupport.GetStrictEncoding(name);

        encoding.DecoderFallback.Should().BeOfType<DecoderExceptionFallback>();
        encoding.EncoderFallback.Should().BeOfType<EncoderExceptionFallback>();
    }

    [Fact]
    public void GetStrictEncoding_Ascii_EncoderThrowsOnUmlaut()
    {
        var act = () => EncodingSupport.GetStrictEncoding("ASCII").GetBytes("ä");

        act.Should().Throw<EncoderFallbackException>("a strict encoding must not write '?' for characters it cannot represent");
    }

    [Theory]
    [InlineData("ANSI")]
    [InlineData("UTF-8-BOM")]
    [InlineData("NOT-AN-ENCODING")]
    public void GetStrictEncoding_UnknownName_ThrowsArgumentException(string name)
    {
        var act = () => EncodingSupport.GetStrictEncoding(name);

        act.Should().Throw<ArgumentException>("callers translate this into their own ConfigurationValidationException");
    }

    [Fact]
    public void EnsureCodePagesRegistered_CalledConcurrently_EveryCallerCanResolveCodePages()
    {
        Parallel.For(0, 32, _ =>
        {
            EncodingSupport.EnsureCodePagesRegistered();
            Encoding.GetEncoding("windows-1252").CodePage.Should().Be(1252);
        });
    }
}
