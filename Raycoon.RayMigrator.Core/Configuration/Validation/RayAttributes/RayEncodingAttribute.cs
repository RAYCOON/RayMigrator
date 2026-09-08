using System.ComponentModel.DataAnnotations;

namespace Raycoon.RayMigrator.Core.Configuration.Validation.RayAttributes;

/// <summary>
/// Attribute for optional validation of connection strings.
/// </summary>
public class RayEncodingAttribute : ValidationAttribute
{
    /// <summary>
    /// Validates whether the provided connection string is valid.
    /// </summary>
    /// <param name="value">The value to be validated.</param>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether validation succeeded or failed.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var memberNames = validationContext.MemberName != null
            ? new[] { validationContext.MemberName }
            : null;

        if (value == null)
        {
            return new ValidationResult($"Invalid encoding string [null] for property [{validationContext.MemberName}]. Please supply a valid Encoding string like 'UTF-8'.", memberNames);
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