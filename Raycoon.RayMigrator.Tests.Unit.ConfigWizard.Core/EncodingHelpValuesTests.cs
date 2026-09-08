using System.Globalization;
using AwesomeAssertions;
using Raycoon.RayMigrator.ConfigWizard.Core.Models;
using Raycoon.RayMigrator.ConfigWizard.Core.Services;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Core;

/// <summary>
/// #4: the field help of ProductDefaults.MigrationFilesEncoding used to list "UTF-8-BOM", a value that neither
/// the wizard's own validator nor the engine accepts. Every advertised value must validate, and the two
/// names users are tempted to type ("UTF-8-BOM", "ANSI") must be rejected with a helpful message.
/// </summary>
public class EncodingHelpValuesTests
{
    private static IEnumerable<string> AdvertisedValues(CultureInfo culture)
    {
        var help = ContextHelpProvider.GetFieldHelp("ProductDefaults_MigrationFilesEncoding", culture);
        help.Should().NotBeNull();
        help!.ValidValues.Should().NotBeNullOrWhiteSpace();
        return help.ValidValues!.Split('|').Select(v => v.Trim()).Where(v => v.Length > 0);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    public void FieldHelp_EveryAdvertisedEncoding_PassesTheWizardValidator(string cultureName)
    {
        var values = AdvertisedValues(new CultureInfo(cultureName)).ToList();
        values.Should().NotBeEmpty();

        foreach (var value in values)
        {
            var result = ConfigurationValidator.ValidateProductDefaults(new ProductDefaultsModel { MigrationFilesEncoding = value });
            result.Errors.Should().NotContain(e => e.Path.Contains("MigrationFilesEncoding"),
                $"the help dialog advertises '{value}', so the validator must accept it (#4)");
        }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    public void FieldHelp_DoesNotAdvertiseUtf8Bom(string cultureName)
    {
        AdvertisedValues(new CultureInfo(cultureName)).Should().NotContain(v => v.Equals("UTF-8-BOM", StringComparison.OrdinalIgnoreCase),
            "a BOM is detected automatically; 'UTF-8-BOM' is not an encoding name");
    }

    [Fact]
    public void ValidateProductDefaults_Windows1252_NoError()
    {
        var result = ConfigurationValidator.ValidateProductDefaults(new ProductDefaultsModel { MigrationFilesEncoding = "windows-1252" });

        result.Errors.Should().NotContain(e => e.Path.Contains("MigrationFilesEncoding"),
            "the wizard registers the code-page provider like the engine does (#4)");
    }

    [Fact]
    public void ValidateProductDefaults_Cp1252Alias_NoError()
    {
        // 'cp1252' resolves only through the code-page provider, unlike 'windows-1252' which a future runtime might know natively
        var result = ConfigurationValidator.ValidateProductDefaults(new ProductDefaultsModel { MigrationFilesEncoding = "cp1252" });

        result.Errors.Should().NotContain(e => e.Path.Contains("MigrationFilesEncoding"));
    }

    [Fact]
    public void FieldHelp_DeAndEnAdvertiseTheSameValues()
    {
        AdvertisedValues(new CultureInfo("de")).Should().Equal(AdvertisedValues(new CultureInfo("en")),
            "the two resource files must not drift apart");
    }

    [Theory]
    [InlineData("UTF-8-BOM")]
    [InlineData("ANSI")]
    public void ValidateProductDefaults_NotAnEncodingName_ReportsErrorNamingValidValues(string value)
    {
        var result = ConfigurationValidator.ValidateProductDefaults(new ProductDefaultsModel { MigrationFilesEncoding = value });

        var error = result.Errors.Should().ContainSingle(e => e.Path.Contains("MigrationFilesEncoding")).Which;
        error.Message.Should().Contain("windows-1252").And.Contain("byte-order mark");
    }
}
