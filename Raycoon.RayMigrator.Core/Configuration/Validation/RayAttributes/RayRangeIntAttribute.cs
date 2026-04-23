// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿using System.ComponentModel.DataAnnotations;

namespace Raycoon.RayMigrator.Core.Configuration.Validation.RayAttributes;

/// <summary>
/// Attribute for validating integer ranges.
/// </summary>
public class RayRangeIntAttribute : ValidationAttribute
{
    private readonly int _minimum;
    private readonly int _maximum;
    private readonly int? _defaultValue = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="RayRangeIntAttribute"/> class.
    /// </summary>
    /// <param name="minimum">The minimum allowable value.</param>
    /// <param name="maximum">The maximum allowable value.</param>
    public RayRangeIntAttribute(int minimum, int maximum)
    {
        _minimum = minimum;
        _maximum = maximum;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RayRangeIntAttribute"/> class.
    /// </summary>
    /// <param name="minimum">The minimum allowable value.</param>
    /// <param name="maximum">The maximum allowable value.</param>
    /// <param name="defaultValue">The default value to use if the input is null.</param>
    public RayRangeIntAttribute(int minimum, int maximum, int defaultValue)
    {
        _minimum = minimum;
        _maximum = maximum;
        _defaultValue = defaultValue;
    }

    /// <summary>
    /// Validates whether the provided value falls within the specified range.
    /// </summary>
    /// <param name="value">The value to be validated.</param>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether validation succeeded or failed.</returns>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var memberNames = validationContext.MemberName != null
            ? new[] {validationContext.MemberName}
            : null;

        try
        {
            if (value is null)
            {
                if (_defaultValue != null)
                {
                    return ValidationContextPropertySetter.SetPropertyValue(_defaultValue, validationContext);
                }

                return new ValidationResult($"{validationContext.DisplayName} is required.", memberNames);
            }

            if (!int.TryParse(value.ToString(), out var intValue))
            {
                return new ValidationResult($"{validationContext.DisplayName} is not a valid number.", memberNames);
            }

            if (intValue < _minimum || intValue > _maximum)
            {
                return new ValidationResult($"{validationContext.DisplayName} must be an integer value between {_minimum} and {_maximum}.", memberNames);
            }

            return ValidationResult.Success;
        }
        catch (Exception ex)
        {
            return new ValidationResult($"Could not process value [{value}] for property [{validationContext.DisplayName}]. Please use a valid configuration-value. Exception: " + ex.Message, memberNames);
        }
    }
}