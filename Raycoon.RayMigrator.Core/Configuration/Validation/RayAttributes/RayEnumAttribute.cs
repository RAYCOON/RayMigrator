using System.ComponentModel.DataAnnotations;
using Raycoon.RayMigrator.Core.Extensions;

namespace Raycoon.RayMigrator.Core.Configuration.Validation.RayAttributes;

public class RayEnumAttribute : ValidationAttribute
{
    private readonly string[] _allowedValues;
    private readonly IReadOnlyDictionary<string, string> _aliases;
    private readonly bool _isRequired;

    public RayEnumAttribute(Type enumType, bool isRequired, bool ignoreFirstValue = true)
    {
        _allowedValues = enumType.AllowedValues(ignoreFirstValue);
        _aliases = enumType.Aliases();
        _isRequired = isRequired;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var errorMessage =
            $"value [{value}] for property [{validationContext.MemberName}]. " +
            $"Allowed values: [{string.Join(", ", _allowedValues)}].";
        
        var memberNames = validationContext.MemberName != null
            ? new[] { validationContext.MemberName }
            : null;

        // If the value is null and required, return "Missing", otherwise return Success
        if (value is null)
        {
            return _isRequired
                ? new ValidationResult("Missing " + errorMessage, memberNames)
                : ValidationResult.Success;
        }

        // At this point, the value is not null.
        // Is it in the list of allowed values, or a former name still accepted as alias (#19)?
        var text = value.ToString()!;
        return _allowedValues.Contains(text, StringComparer.OrdinalIgnoreCase) || _aliases.ContainsKey(text)
            ? ValidationResult.Success
            : new ValidationResult("Invalid " + errorMessage, memberNames);
    }
}