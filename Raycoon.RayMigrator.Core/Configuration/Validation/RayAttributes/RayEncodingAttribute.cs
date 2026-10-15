using System.ComponentModel.DataAnnotations;

namespace Raycoon.RayMigrator.Core.Configuration.Validation.RayAttributes;

/// <summary>
/// Validates the name of a migration files encoding. A value that is left out or empty is set to
/// <see cref="EncodingSupport.DefaultEncodingName"/> (UTF-8), the way <see cref="RayRangeIntAttribute"/> writes its
/// default into an empty number, so that the prepared options always name the encoding they read with; a value
/// that is not a .NET encoding name fails.
/// </summary>
public class RayEncodingAttribute : ValidationAttribute
{
    /// <summary>The encoding name written into a value that is left out or empty.</summary>
    public string DefaultValue => EncodingSupport.DefaultEncodingName;

    /// <summary>
    /// Validates the encoding name; writes the default into a missing or empty value.
    /// </summary>
    /// <param name="value">The value to be validated.</param>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether validation succeeded or failed.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var memberNames = validationContext.MemberName != null
            ? new[] { validationContext.MemberName }
            : null;

        if (value == null || (value is string text && string.IsNullOrWhiteSpace(text)))
        {
            return ValidationContextPropertySetter.SetPropertyValue(DefaultValue, validationContext);
        }

        if (value is string encodingString)
        {
            try
            {
                _ = EncodingSupport.GetStrictEncoding(encodingString);
                return ValidationResult.Success;
            }
            catch (Exception)
            {
                return new ValidationResult($"Invalid encoding string [{value}] for property [{validationContext.MemberName}]. {EncodingSupport.ValidNamesHint}", memberNames);
            }
        }

        return ValidationResult.Success;
    }
}
