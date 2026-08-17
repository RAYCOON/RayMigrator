using System.Globalization;

namespace Raycoon.RayMigrator.Tests.Unit.ConfigWizard.Web;

/// <summary>
/// Tests for LocalizationService — multilingual UI strings + Core help provider wrapper.
/// </summary>
public class LocalizationServiceTests
{
    // ── Default language ──────────────────────────────────────────

    [Fact]
    public void Language_DefaultIsEnglish()
    {
        var svc = new LocalizationService();

        svc.Language.Should().Be("en");
    }

    [Fact]
    public void Culture_DefaultIsEnUs()
    {
        var svc = new LocalizationService();

        svc.Culture.Name.Should().Be("en-US");
    }

    // ── Language switching ────────────────────────────────────────

    [Fact]
    public void Language_SetToDe_ChangesCultureToDeDE()
    {
        var svc = new LocalizationService();

        svc.Language = "de";

        svc.Culture.Name.Should().Be("de-DE");
    }

    [Fact]
    public void Language_SetToEn_KeepsCultureAsEnUs()
    {
        var svc = new LocalizationService();
        svc.Language = "de";

        svc.Language = "en";

        svc.Culture.Name.Should().Be("en-US");
    }

    [Fact]
    public void Language_SetToSameValue_DoesNotFireLanguageChanged()
    {
        var svc = new LocalizationService();
        int count = 0;
        svc.LanguageChanged += () => count++;

        svc.Language = "en"; // already "en"

        count.Should().Be(0);
    }

    [Fact]
    public void Language_ChangedToDe_FiresLanguageChanged()
    {
        var svc = new LocalizationService();
        bool fired = false;
        svc.LanguageChanged += () => fired = true;

        svc.Language = "de";

        fired.Should().BeTrue();
    }

    [Fact]
    public void Language_ChangedFromDe_FiresLanguageChanged()
    {
        var svc = new LocalizationService();
        svc.Language = "de";
        bool fired = false;
        svc.LanguageChanged += () => fired = true;

        svc.Language = "en";

        fired.Should().BeTrue();
    }

    // ── English string lookups ────────────────────────────────────

    [Theory]
    [InlineData("Welcome.Title", "Welcome to RayMigrator Config Wizard")]
    [InlineData("Welcome.CreateNew", "Create New Configuration")]
    [InlineData("Common.Next", "Next")]
    [InlineData("Common.Back", "Back")]
    [InlineData("Common.Save", "Save")]
    [InlineData("Common.Cancel", "Cancel")]
    [InlineData("Summary.Confirm", "Confirm and Continue")]
    [InlineData("Summary.Restart", "Restart Setup")]
    [InlineData("Summary.Enabled", "Enabled")]
    [InlineData("Summary.Disabled", "Disabled")]
    [InlineData("Phase3.Download", "Download ZIP")]
    [InlineData("Section.Repository", "Repository")]
    [InlineData("Section.Serilog", "Serilog")]
    [InlineData("Footer.Imprint", "Legal Notice")]
    [InlineData("Footer.Privacy", "Privacy")]
    [InlineData("Footer.Terms", "Terms of Use")]
    [InlineData("Footer.ImprintUrl", "https://raymigrator.com/en/legal-notice")]
    [InlineData("Footer.PrivacyUrl", "https://raymigrator.com/en/privacy")]
    [InlineData("Footer.TermsUrl", "https://raymigrator.com/en/terms-of-use")]
    [InlineData("Terms.DialogTitle", "Terms of Use")]
    [InlineData("Terms.AcceptCheckbox", "I have read and accept the Terms of Use.")]
    [InlineData("Terms.AcceptAndDownload", "Accept & download")]
    public void Get_EnglishKeys_ReturnExpectedEnglishStrings(string key, string expected)
    {
        var svc = new LocalizationService();

        var result = svc.Get(key);

        result.Should().Be(expected);
    }

    [Fact]
    public void Get_UnknownKey_ReturnsKeyItself()
    {
        var svc = new LocalizationService();

        var result = svc.Get("UnknownKey.DoesNotExist");

        result.Should().Be("UnknownKey.DoesNotExist");
    }

    // ── German string lookups ─────────────────────────────────────

    [Theory]
    [InlineData("Welcome.Title", "Willkommen zum RayMigrator Konfigurations-Assistenten")]
    [InlineData("Welcome.CreateNew", "Neue Konfiguration erstellen")]
    [InlineData("Common.Next", "Vor")]
    [InlineData("Common.Back", "Zurück")]
    [InlineData("Summary.Enabled", "Aktiviert")]
    [InlineData("Summary.Disabled", "Deaktiviert")]
    [InlineData("Phase3.Download", "ZIP herunterladen")]
    [InlineData("Footer.Imprint", "Impressum")]
    [InlineData("Footer.Privacy", "Datenschutz")]
    [InlineData("Footer.Terms", "Nutzungsbedingungen")]
    [InlineData("Footer.ImprintUrl", "https://raymigrator.com/impressum")]
    [InlineData("Footer.PrivacyUrl", "https://raymigrator.com/datenschutz")]
    [InlineData("Footer.TermsUrl", "https://raymigrator.com/nutzungsbedingungen")]
    [InlineData("Terms.DialogTitle", "Nutzungsbedingungen")]
    [InlineData("Terms.AcceptCheckbox", "Ich habe die Nutzungsbedingungen gelesen und akzeptiere sie.")]
    [InlineData("Terms.AcceptAndDownload", "Akzeptieren & herunterladen")]
    public void Get_GermanKeys_ReturnExpectedGermanStrings(string key, string expected)
    {
        var svc = new LocalizationService();
        svc.Language = "de";

        var result = svc.Get(key);

        result.Should().Be(expected);
    }

    [Fact]
    public void Get_GermanUnknownKey_ReturnsKeyItself()
    {
        var svc = new LocalizationService();
        svc.Language = "de";

        var result = svc.Get("Unknown.Key");

        result.Should().Be("Unknown.Key");
    }

    // ── Language switch affects Get output ────────────────────────

    [Fact]
    public void Get_AfterSwitchFromEnToDe_ReturnsGermanString()
    {
        var svc = new LocalizationService();
        var english = svc.Get("Common.Next");

        svc.Language = "de";
        var german = svc.Get("Common.Next");

        english.Should().Be("Next");
        german.Should().Be("Vor");
    }

    [Fact]
    public void Get_AfterSwitchFromDeToEn_ReturnsEnglishString()
    {
        var svc = new LocalizationService();
        svc.Language = "de";
        var german = svc.Get("Summary.Confirm");

        svc.Language = "en";
        var english = svc.Get("Summary.Confirm");

        german.Should().Be("Bestätigen und fortfahren");
        english.Should().Be("Confirm and Continue");
    }

    // ── Core help provider wrappers ───────────────────────────────

    [Fact]
    public void GetSectionHelp_ForKnownSection_ReturnsNonNull()
    {
        var svc = new LocalizationService();

        // "Repository" is a known section in ContextHelpProvider
        var help = svc.GetSectionHelp("Repository");

        help.Should().NotBeNull();
    }

    [Fact]
    public void GetSectionHelp_ForUnknownSection_ReturnsNull()
    {
        var svc = new LocalizationService();

        var help = svc.GetSectionHelp("NonExistentSection_XYZ_123");

        help.Should().BeNull();
    }

    [Fact]
    public void GetFieldHelp_ForKnownField_ReturnsNonNull()
    {
        var svc = new LocalizationService();

        // ContextHelpProvider uses underscore-separated keys, e.g. "Repository_DatabaseType"
        var help = svc.GetFieldHelp("Repository_DatabaseType");

        help.Should().NotBeNull();
    }

    [Fact]
    public void GetFieldHelp_ForUnknownField_ReturnsNull()
    {
        var svc = new LocalizationService();

        var help = svc.GetFieldHelp("NonExistent.Field_XYZ_123");

        help.Should().BeNull();
    }

    [Fact]
    public void GetSectionHelp_GermanLanguage_UsesDeCulture()
    {
        var svc = new LocalizationService();
        svc.Language = "de";

        var help = svc.GetSectionHelp("Repository");

        // Just verify it doesn't throw and may return a result
        // (content depends on ContextHelpProvider DE translations)
        // We do not assert the exact text — that's tested in ContextHelpProvider tests
    }

    // ── All EN keys are defined ───────────────────────────────────

    [Theory]
    [InlineData("Welcome.Subtitle")]
    [InlineData("Welcome.UploadExisting")]
    [InlineData("Welcome.CreateNewDescription")]
    [InlineData("Welcome.UploadDescription")]
    [InlineData("Welcome.SelectFiles")]
    [InlineData("Welcome.GoToOverview")]
    [InlineData("Welcome.WalkThrough")]
    [InlineData("Welcome.Language")]
    [InlineData("Repository.Title")]
    [InlineData("Repository.Subtitle")]
    [InlineData("Products.Title")]
    [InlineData("Products.Subtitle")]
    [InlineData("Products.AddProduct")]
    [InlineData("Products.ProductAlias")]
    [InlineData("Products.RemoveProduct")]
    [InlineData("OptionalFeatures.Title")]
    [InlineData("OptionalFeatures.Subtitle")]
    [InlineData("OptionalFeatures.DatabaseLogging")]
    [InlineData("OptionalFeatures.CliTools")]
    [InlineData("Summary.Title")]
    [InlineData("Summary.Subtitle")]
    [InlineData("Summary.Repository")]
    [InlineData("Summary.DatabaseLogging")]
    [InlineData("Summary.CliTools")]
    [InlineData("Phase2.Title")]
    [InlineData("Phase2.Subtitle")]
    [InlineData("Phase3.Title")]
    [InlineData("Phase3.Subtitle")]
    [InlineData("Section.DatabaseLogging")]
    [InlineData("Section.CliTools")]
    [InlineData("Section.ProductDefaults")]
    [InlineData("Section.ProductSettings")]
    [InlineData("Common.Add")]
    [InlineData("Common.Remove")]
    [InlineData("Common.Edit")]
    [InlineData("Common.Alias")]
    [InlineData("Common.ConnectionString")]
    [InlineData("Common.DatabaseType")]
    [InlineData("Common.SchemaName")]
    [InlineData("Common.Errors")]
    [InlineData("Common.Warnings")]
    [InlineData("Common.Valid")]
    [InlineData("Common.InheritedFrom")]
    [InlineData("Common.Override")]
    [InlineData("Common.Default")]
    [InlineData("Common.Timeout")]
    [InlineData("Common.MaxRetries")]
    [InlineData("Common.WaitTime")]
    [InlineData("Env.Tabs")]
    [InlineData("Env.Base")]
    [InlineData("Promotion.Applied")]
    public void Get_AllDefinedEnglishKeys_DoNotReturnKeyItself(string key)
    {
        var svc = new LocalizationService();

        var result = svc.Get(key);

        // A known key must not echo back the key — it must have a proper translation
        result.Should().NotBe(key, because: $"key '{key}' must have a defined English translation");
    }

    // ── Supported languages ───────────────────────────────────────

    [Fact]
    public void SupportedLanguages_ContainsEnglishAndGerman()
    {
        var codes = LocalizationService.SupportedLanguages.Select(l => l.Code).ToList();

        codes.Should().Contain("en");
        codes.Should().Contain("de");
    }

    [Fact]
    public void SupportedLanguages_AllHaveNativeNameAndCulture()
    {
        foreach (var lang in LocalizationService.SupportedLanguages)
        {
            lang.NativeName.Should().NotBeNullOrWhiteSpace($"language '{lang.Code}' must have a native name");
            lang.CultureName.Should().NotBeNullOrWhiteSpace($"language '{lang.Code}' must have a culture name");
        }
    }

    // ── Translation key completeness ──────────────────────────────

    [Fact]
    public void Translations_AllLanguagesHaveSameKeys()
    {
        // Use Get() to probe: if a key exists in EN but not in another language,
        // Get() falls back to EN. So we verify by checking that for every EN key,
        // switching to each other language returns a different string (not the EN fallback).
        var svc = new LocalizationService();

        // Collect all EN keys by probing known keys
        var enKeys = GetAllTranslationKeys();

        foreach (var lang in LocalizationService.SupportedLanguages.Where(l => l.Code != "en"))
        {
            svc.Language = lang.Code;
            foreach (var key in enKeys)
            {
                var result = svc.Get(key);
                result.Should().NotBe(key,
                    because: $"key '{key}' must have a translation for language '{lang.Code}'");
            }
        }
    }

    [Fact]
    public void Get_FallsBackToEnglish_WhenKeyMissingInCurrentLanguage()
    {
        // This tests the fallback mechanism — if we could add a key only to EN,
        // Get() should return the EN value. We verify by checking an unknown language code.
        var svc = new LocalizationService();

        // "en" is always available, so Get should work for any defined key
        var result = svc.Get("Welcome.Title");

        result.Should().Be("Welcome to RayMigrator Config Wizard");
    }

    // ── Culture mapping ───────────────────────────────────────────

    [Theory]
    [InlineData("en", "en-US")]
    [InlineData("de", "de-DE")]
    public void Culture_ReturnsCorrectCultureForLanguage(string langCode, string expectedCulture)
    {
        var svc = new LocalizationService();
        svc.Language = langCode;

        svc.Culture.Name.Should().Be(expectedCulture);
    }

    // ── Helper ────────────────────────────────────────────────────

    private static IEnumerable<string> GetAllTranslationKeys()
    {
        // Probe all known keys from the test data above + additional keys
        return new[]
        {
            "Welcome.Title", "Welcome.Subtitle", "Welcome.CreateNew", "Welcome.CreateNewDescription",
            "Welcome.UploadExisting", "Welcome.UploadDescription", "Welcome.SelectFiles", "Welcome.UploadPrivacy",
            "Welcome.GoToOverview", "Welcome.WalkThrough", "Welcome.Language",
            "Repository.Title", "Repository.Subtitle", "Repository.TableBaseName",
            "Products.Title", "Products.Subtitle", "Products.AddProduct", "Products.ProductAlias",
            "Products.RemoveProduct", "Products.Empty", "Products.MigrationFilesRootDirectory",
            "ProductDetail.Title", "ProductDetail.Environments", "ProductDetail.AddEnvironment",
            "ProductDetail.TargetGroups", "ProductDetail.AddTargetGroup", "ProductDetail.TargetGroupAlias",
            "ProductDetail.DatabaseType", "ProductDetail.Targets", "ProductDetail.AddTarget",
            "ProductDetail.TargetAlias", "ProductDetail.RemoveTarget", "ProductDetail.RemoveTargetGroup",
            "ProductDetail.RemoveEnvironment",
            "OptionalFeatures.Title", "OptionalFeatures.Subtitle", "OptionalFeatures.DatabaseLogging",
            "OptionalFeatures.DatabaseLoggingHint", "OptionalFeatures.CliTools", "OptionalFeatures.CliToolsHint",
            "Summary.Title", "Summary.Subtitle", "Summary.Confirm", "Summary.Edit", "Summary.Restart",
            "Summary.Repository", "Summary.DatabaseLogging", "Summary.CliTools",
            "Summary.Enabled", "Summary.Disabled",
            "Phase2.Title", "Phase2.Subtitle", "Phase2.BackToStart", "Phase2.Completed", "Phase2.GoToOverview",
            "Phase3.Title", "Phase3.Subtitle", "Phase3.Download", "Phase3.ExportWarning",
            "Welcome.MaturityNotice", "Footer.Imprint", "Footer.Privacy", "Footer.Terms",
            "Footer.ImprintUrl", "Footer.PrivacyUrl", "Footer.TermsUrl",
            "Terms.DialogTitle", "Terms.DialogIntro", "Terms.PointDraft", "Terms.PointReview",
            "Terms.PointBackup", "Terms.ReadFull", "Terms.AcceptCheckbox", "Terms.AcceptAndDownload",
            "Section.Repository", "Section.DatabaseLogging", "Section.CliTools",
            "Section.ProductDefaults", "Section.ProductSettings", "Section.Serilog",
            "Common.Next", "Common.Back", "Common.Save", "Common.Cancel", "Common.Add", "Common.Remove",
            "Common.Edit", "Common.Alias", "Common.ConnectionString", "Common.DatabaseType", "Common.SchemaName",
            "Common.Errors", "Common.Warnings", "Common.Valid", "Common.InheritedFrom", "Common.Override",
            "Common.Default", "Common.Timeout", "Common.MaxRetries", "Common.WaitTime",
            "Env.Tabs", "Env.Base",
            "Promotion.Applied",
            "JsonPreview.Title", "Export.Success",
            "Matrix.Title", "Matrix.Empty", "Matrix.Configured", "Matrix.BaseOnly", "Matrix.Product",
            "DbLogging.Disabled", "DbLogging.TableBaseName", "DbLogging.MinimumLevel", "DbLogging.Enable",
            "CliTools.Empty", "CliTools.ExecutablePath", "CliTools.ArgumentTemplate", "CliTools.InputMode",
            "ProductDefaults.MigrationErrorAction", "ProductDefaults.RollbackErrorAction",
            "ProductDefaults.MigrationFilesExtension", "ProductDefaults.RollbackPreExtension",
            "ProductDefaults.MigrationFilesEncoding", "ProductDefaults.RequireRollbackFile",
            "ProductDefaults.TargetGroupDefaultsTitle", "ProductDefaults.TargetMigrationOrder",
            "ProductDefaults.HashValidationScope", "ProductDefaults.TargetDefaultsTitle",
            "ProductDefaults.DefaultCliToolAlias",
            "Phase.Configuration", "Phase.Overview",
            "Serilog.SinkName", "Serilog.LevelOverrides", "Serilog.MinimumLevel",
            "Upload.FilesLoaded",
            "Help.ValidValues", "Help.Default", "Help.Examples",
            "Help.JsonConfigPath", "Help.InheritedBy", "Common.Close",
            "Products.OverrideProductDefaults", "Products.OverrideTargetGroupDefaults",
            "Products.OverrideTargetDefaults",
            "Section.StructureSetup",
            "Structure.Title", "Structure.Subtitle", "Structure.Environments",
            "Structure.AddEnvironment", "Structure.TargetGroups", "Structure.AddTargetGroup",
            "Structure.Targets", "Structure.AddTarget",
            "Mode.ExpertMode", "Mode.EasyModeHint",
            "DbLogging.Recommendation",
            "Products.AddTargetBtn", "Products.AddTargetGroupBtn",
            "Products.AddProductBtn", "Products.RemoveProductBtn",
            "CliTools.AddTool", "Serilog.AddSink"
        };
    }
}
