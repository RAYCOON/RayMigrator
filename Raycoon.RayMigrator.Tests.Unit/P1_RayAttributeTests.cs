
using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration.Validation.RayAttributes;

namespace Raycoon.RayMigrator.Tests.Unit;

/// <summary>
/// P1-3: Configuration Validation (RayAttributes) tests.
/// Invalid configurations passing validation cause runtime crashes.
/// </summary>
public class RayEnumAttributeTests
{
    private static ValidationResult? Validate(RayEnumAttribute attr, object? value, string memberName = "TestProp")
    {
        var context = new ValidationContext(new object()) { MemberName = memberName };
        return attr.GetValidationResult(value, context);
    }

    [Fact]
    public void ValidEnumValue_ReturnsSuccess()
    {
        var attr = new RayEnumAttribute(typeof(MigrationErrorAction), isRequired: true);
        var result = Validate(attr, "Terminate");

        result.Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void InvalidEnumValue_ReturnsError()
    {
        var attr = new RayEnumAttribute(typeof(MigrationErrorAction), isRequired: true);
        var result = Validate(attr, "InvalidValue");

        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("Invalid");
    }

    [Fact]
    public void NullAndRequired_ReturnsMissingError()
    {
        var attr = new RayEnumAttribute(typeof(MigrationErrorAction), isRequired: true);
        var result = Validate(attr, null);

        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("Missing");
    }

    [Fact]
    public void NullAndOptional_ReturnsSuccess()
    {
        var attr = new RayEnumAttribute(typeof(MigrationErrorAction), isRequired: false);
        var result = Validate(attr, null);

        result.Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void CaseInsensitive_ReturnsSuccess()
    {
        var attr = new RayEnumAttribute(typeof(MigrationErrorAction), isRequired: true);
        var result = Validate(attr, "terminate");

        result.Should().Be(ValidationResult.Success);
    }
}

public class RayRangeIntAttributeTests
{
    private static ValidationResult? Validate(RayRangeIntAttribute attr, object? value, object instance, string memberName)
    {
        var context = new ValidationContext(instance) { MemberName = memberName };
        return attr.GetValidationResult(value, context);
    }

    [Fact]
    public void ValueInRange_ReturnsSuccess()
    {
        var attr = new RayRangeIntAttribute(0, 100, 50);
        var target = new TargetDefaultsOptions();
        var result = Validate(attr, 50, target, nameof(TargetDefaultsOptions.DbCommandTimeoutInSeconds));

        result.Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void ValueBelowMinimum_ReturnsError()
    {
        var attr = new RayRangeIntAttribute(10, 100);
        var target = new TargetDefaultsOptions();
        var result = Validate(attr, 5, target, nameof(TargetDefaultsOptions.DbCommandTimeoutInSeconds));

        result.Should().NotBe(ValidationResult.Success);
    }

    [Fact]
    public void ValueAboveMaximum_ReturnsError()
    {
        var attr = new RayRangeIntAttribute(0, 100);
        var target = new TargetDefaultsOptions();
        var result = Validate(attr, 150, target, nameof(TargetDefaultsOptions.DbCommandTimeoutInSeconds));

        result.Should().NotBe(ValidationResult.Success);
    }

    [Fact]
    public void NullWithDefault_SetsDefault()
    {
        var attr = new RayRangeIntAttribute(0, int.MaxValue, 20);
        var target = new TargetDefaultsOptions();
        var result = Validate(attr, null, target, nameof(TargetDefaultsOptions.DbCommandTimeoutInSeconds));

        result.Should().Be(ValidationResult.Success);
        target.DbCommandTimeoutInSeconds.Should().Be(20);
    }

    [Fact]
    public void NullWithoutDefault_ReturnsError()
    {
        var attr = new RayRangeIntAttribute(0, 100);
        var target = new TargetDefaultsOptions();
        var result = Validate(attr, null, target, nameof(TargetDefaultsOptions.DbCommandTimeoutInSeconds));

        result.Should().NotBe(ValidationResult.Success);
    }

    [Fact]
    public void BoundaryMinimum_ReturnsSuccess()
    {
        var attr = new RayRangeIntAttribute(0, 100, 50);
        var target = new TargetDefaultsOptions();
        var result = Validate(attr, 0, target, nameof(TargetDefaultsOptions.DbCommandTimeoutInSeconds));

        result.Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void BoundaryMaximum_ReturnsSuccess()
    {
        var attr = new RayRangeIntAttribute(0, 100, 50);
        var target = new TargetDefaultsOptions();
        var result = Validate(attr, 100, target, nameof(TargetDefaultsOptions.DbCommandTimeoutInSeconds));

        result.Should().Be(ValidationResult.Success);
    }
}

public class RayConnectionStringAttributeTests
{
    private static ValidationResult? Validate(RayConnectionStringAttribute attr, object? value, string memberName = "ConnectionString")
    {
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var context = new ValidationContext(new object(), sp, null) { MemberName = memberName };
        return attr.GetValidationResult(value, context);
    }

    [Fact]
    public void ValidConnectionString_ReturnsSuccess()
    {
        var attr = new RayConnectionStringAttribute(false);
        var result = Validate(attr, "Server=localhost;Database=TestDB;Trusted_Connection=True;");

        result.Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void InvalidConnectionString_ReturnsError()
    {
        var attr = new RayConnectionStringAttribute(false);
        // A string with unmatched quotes is typically invalid
        var result = Validate(attr, "invalid=\"unterminated");

        result.Should().NotBe(ValidationResult.Success);
    }

    [Fact]
    public void EmptyAndRequired_ReturnsError()
    {
        var attr = new RayConnectionStringAttribute(true);
        var result = Validate(attr, "");

        result.Should().NotBe(ValidationResult.Success);
    }

    [Fact]
    public void NullWithRequired_ReturnsError()
    {
        var attr = new RayConnectionStringAttribute(true);
        var result = Validate(attr, null);

        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("Invalid type");
    }

    [Fact]
    public void NullWithOptional_ReturnsError()
    {
        var attr = new RayConnectionStringAttribute(false);
        var result = Validate(attr, null);

        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("Invalid type");
    }
}

public class RayEncodingAttributeTests
{
    private static ValidationResult? Validate(RayEncodingAttribute attr, object? value, string memberName = "Encoding")
    {
        var context = new ValidationContext(new object()) { MemberName = memberName };
        return attr.GetValidationResult(value, context);
    }

    [Fact]
    public void ValidEncoding_Utf8_ReturnsSuccess()
    {
        var attr = new RayEncodingAttribute();
        var result = Validate(attr, "UTF-8");

        result.Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void ValidEncoding_Ascii_ReturnsSuccess()
    {
        var attr = new RayEncodingAttribute();
        var result = Validate(attr, "ASCII");

        result.Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void InvalidEncoding_ReturnsError()
    {
        var attr = new RayEncodingAttribute();
        var result = Validate(attr, "NOT-AN-ENCODING");

        result.Should().NotBe(ValidationResult.Success);
    }

    [Fact]
    public void NullInput_ReturnsError()
    {
        var attr = new RayEncodingAttribute();
        var result = Validate(attr, null);

        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("null");
    }
}

public class RayDirectoryExistsAttributeTests
{
    private static ValidationResult? Validate(RayDirectoryExistsAttribute attr, object? value, string memberName = "Directory")
    {
        var context = new ValidationContext(new object()) { MemberName = memberName };
        return attr.GetValidationResult(value, context);
    }

    [Fact]
    public void ExistingDirectory_ReturnsSuccess()
    {
        var attr = new RayDirectoryExistsAttribute();
        var result = Validate(attr, Path.GetTempPath());

        result.Should().Be(ValidationResult.Success);
    }

    [Fact]
    public void NonExistingDirectory_ReturnsError()
    {
        var attr = new RayDirectoryExistsAttribute();
        var nonExistent = Path.Combine(Path.GetTempPath(), "nonexistent_dir_xyz_" + Guid.NewGuid());
        var result = Validate(attr, nonExistent);

        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("does not exist");
    }

    [Fact]
    public void NonStringInput_ReturnsError()
    {
        var attr = new RayDirectoryExistsAttribute();
        var result = Validate(attr, 12345);

        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("not a valid string");
    }

    [Fact]
    public void NullInput_ReturnsError()
    {
        var attr = new RayDirectoryExistsAttribute();
        var result = Validate(attr, null);

        result.Should().NotBe(ValidationResult.Success);
        result!.ErrorMessage.Should().Contain("not a valid string");
    }
}

/// <summary>
/// Helper to allow constructing ServiceProvider for RayConnectionString tests.
/// </summary>
file class ServiceCollection : Microsoft.Extensions.DependencyInjection.ServiceCollection { }
