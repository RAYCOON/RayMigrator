// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Microsoft.Extensions.DependencyInjection;
using Raycoon.RayMigrator.Core.Configuration.Options;

namespace Raycoon.RayMigrator.Core.Configuration.Validation.RayAttributes;

/// <summary>
/// Attribute for optional validation of connection strings.
/// </summary>
public class RayConnectionStringAttribute : ValidationAttribute
{
    private readonly bool _isRequired;

    public RayConnectionStringAttribute(bool isRequired)
    {
        _isRequired = isRequired;
    }
    
    /// <summary>
    /// Validates whether the provided connection string is valid.
    /// </summary>
    /// <param name="value">The value to be validated.</param>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether validation succeeded or failed.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var consoleOptions = validationContext.GetService<RayMigratorConsoleOptions>();
        bool revealSensitiveData = consoleOptions?.RevealSensitiveData ?? false;

        if (value is string connectionString)
        {
            string[]? memberNames;
            
            try
            {
                if (_isRequired && string.IsNullOrWhiteSpace(connectionString))
                {
                    memberNames = validationContext.MemberName != null
                        ? new[] {validationContext.MemberName}
                        : null;

                    return new ValidationResult($"Missing property [{validationContext.MemberName}] with value [{connectionString}].", memberNames);
                }

                _ = new DbConnectionStringBuilder {ConnectionString = connectionString};
                return ValidationResult.Success!;
            }
            catch (Exception ex)
            {
                memberNames = validationContext.MemberName != null
                    ? new[] {validationContext.MemberName}
                    : null;

                if (revealSensitiveData)
                {
                    return new ValidationResult($"Invalid structure of property [{validationContext.MemberName}] with value [{connectionString}]. Exception: " + ex.Message, memberNames);
                }

                return new ValidationResult($"Invalid structure of [{validationContext.MemberName}]-value. Exception: " + ex.Message, memberNames);
            }
            
        }
        return new ValidationResult($"Invalid type of property [{validationContext.MemberName}]. Property is not a string value.");
    }
}