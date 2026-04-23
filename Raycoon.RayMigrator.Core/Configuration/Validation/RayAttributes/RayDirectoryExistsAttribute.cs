// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿using System.ComponentModel.DataAnnotations;
using Raycoon.RayMigrator.Core.Extensions;

namespace Raycoon.RayMigrator.Core.Configuration.Validation.RayAttributes;

/// <summary>
/// Attribute for required validation of directory existence.
/// </summary>
public class RayDirectoryExistsAttribute : ValidationAttribute
{
    /// <summary>
    /// Validates whether the provided directory path exists, handling paths consistently across platforms. 
    /// </summary>
    /// <param name="value">The value to be validated.</param>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether validation succeeded or failed.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var memberNames = validationContext.MemberName != null
            ? new[] {validationContext.MemberName}
            : null;

        if (value is not string directoryString)
        {
            return new ValidationResult($"Configured directory [{value}] is not a valid string.", memberNames);
        }

        string initialDirectoryString = directoryString;

        try
        {
            directoryString = directoryString.NormalizePath();


            // Remove trailing directory separator
            directoryString = Path.TrimEndingDirectorySeparator(directoryString);

            // Handle relative paths
            if (!Path.IsPathRooted(directoryString))
            {
                string baseDir = AppContext.BaseDirectory.NormalizePath();
                directoryString = Path.Combine(baseDir, directoryString);
            }

            // Get the full path with normalized separators
            directoryString = Path.GetFullPath(directoryString);

            if (!Directory.Exists(directoryString))
            {
                string dirAddon = (initialDirectoryString == directoryString) ? "" : $"[{initialDirectoryString}] ==> ";
                return new ValidationResult($"Configured directory [{value}] is not valid. The resolved directory {dirAddon}[{directoryString}] does not exist.", memberNames);
            }

            // Update property if path was modified
            if (initialDirectoryString != directoryString)
            {
                try
                {
                    ValidationContextPropertySetter.SetPropertyValue(directoryString, validationContext);
                }
                catch (Exception ex)
                {
                    return new ValidationResult($"Internal error: Could not set new property value for directory [{initialDirectoryString}] ==> [{directoryString}]. Exception: " + ex.Message, memberNames);
                }
            }

            return ValidationResult.Success;
        }
        catch (Exception ex)
        {
            return new ValidationResult($"Internal error: Could not check existence for directory [{initialDirectoryString}] ==> [{directoryString}]. Exception: " + ex.Message, memberNames);
        }
    }
}