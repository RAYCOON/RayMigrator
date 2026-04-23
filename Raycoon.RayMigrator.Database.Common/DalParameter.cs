// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿namespace Raycoon.RayMigrator.Database.Common;

public class DalParameter
{
    public string ParameterName { get; set; }
    public object? ParameterValue { get; set; }
    public Type ParameterType { get; set; }

    public DalParameter(string name, object? value, Type type)
    {
        ParameterName = name;
        ParameterValue = value;
        ParameterType = type;
    }

    public override string ToString()
    {
        return (ParameterValue ?? "NULL") + $" ({ParameterType})";
    }
}