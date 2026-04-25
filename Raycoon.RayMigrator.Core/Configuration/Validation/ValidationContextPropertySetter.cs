using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Raycoon.RayMigrator.Core.Configuration.Validation;

public static class ValidationContextPropertySetter
{
    /// <summary>
    /// Sets a property with a new value for a given ValidationContext.
    /// </summary>
    /// <param name="variableValue">The new value of the property.</param>
    /// <param name="validationContext">The validation context.</param>
    /// <returns>A ValidationResult indicating success or failure.</returns>
    public static ValidationResult? SetPropertyValue(object? variableValue, ValidationContext validationContext)
    {
        //if (variableValue == null) throw new ArgumentException("Value of variable may not be null", nameof(variableValue));
        if (validationContext == null) throw new ArgumentException("ValidationContext may not be null", nameof(validationContext));
        
        var memberNames = validationContext.MemberName != null
            ? new[] { validationContext.MemberName }
            : null;
        
        PropertyInfo? property;
        try
        {
            property = validationContext.ObjectType.GetProperty(validationContext.MemberName!);
        }
        catch (Exception ex)
        {
            return new ValidationResult($"Internal Error: Could not set property [{validationContext.DisplayName}]. Exception: {ex}", memberNames);
        }
        
        if (property != null && property.CanWrite)
        {
            try
            {
                Type propertyType = property.PropertyType;

                try
                {
                    object? convertedValue;
                    if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        // Handle nullable types
                        if (variableValue == null)
                        {
                            convertedValue = null;
                        }
                        else
                        {
                            // Get the underlying type of the nullable type
                            Type underlyingType = Nullable.GetUnderlyingType(propertyType)!;
                            // Perform the conversion
                            convertedValue = Convert.ChangeType(variableValue, underlyingType);
                        }
                    }
                    else
                    {
                        // Handle non-nullable types
                        convertedValue = Convert.ChangeType(variableValue, propertyType);
                    }

                    property.SetValue(validationContext.ObjectInstance, convertedValue);
                }
                catch (InvalidCastException ex)
                {
                    return new ValidationResult($"Internal Error: The {validationContext.DisplayName} could not be set from given object-value: Invalid cast from '{variableValue?.GetType().FullName}' to '{propertyType.FullName}'. Error: {ex.Message}", memberNames);
                }

                return ValidationResult.Success;
            }
            catch (Exception ex)
            {
                return new ValidationResult($"Internal Error: The {validationContext.DisplayName} could not be set. Error: {ex.Message}", memberNames);
            }
        }

        return new ValidationResult($"Internal Error: Could not set property [{validationContext.DisplayName}] from given object-value.", memberNames);
    }
}
