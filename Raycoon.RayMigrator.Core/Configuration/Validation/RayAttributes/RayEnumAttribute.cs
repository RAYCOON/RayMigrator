// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿using System.ComponentModel.DataAnnotations;
using Raycoon.RayMigrator.Core.Extensions;

namespace Raycoon.RayMigrator.Core.Configuration.Validation.RayAttributes;

public class RayEnumAttribute : ValidationAttribute
{
    private readonly string[] _allowedValues;
    private readonly bool _isRequired;

    public RayEnumAttribute(Type enumType, bool isRequired, bool ignoreFirstValue = true)
    {
        _allowedValues = enumType.AllowedValues(ignoreFirstValue);
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
        // Is it in the list of allowed values?
        return _allowedValues.Contains(value.ToString()!, StringComparer.OrdinalIgnoreCase)
            ? ValidationResult.Success
            : new ValidationResult("Invalid " + errorMessage, memberNames);
    }
}