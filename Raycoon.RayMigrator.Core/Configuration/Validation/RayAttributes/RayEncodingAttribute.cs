// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿using System.ComponentModel.DataAnnotations;
using System.Text;

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
                _ = Encoding.GetEncoding(encodingString);
                return ValidationResult.Success;
            }
            catch(Exception ex)
            {
                return new ValidationResult($"Invalid encoding string [{value}] for property [{validationContext.MemberName}]. Please use a valid encoding string like 'UTF-8'. Some encodings (e.g. 'windows-1252') require System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance) on .NET Core. Exception: " + ex.Message, memberNames);
            }
        }
        
        return ValidationResult.Success;
    }
}