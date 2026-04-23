// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿namespace Raycoon.RayMigrator.Core.Configuration.Replacer;

public class EnvironmentVariableWithMetadata
{
    public string Path { get; set; } = null!;
    public string ConfigurationKey { get; set; } = null!;
    public string? ConfigurationValue { get; set; }
    public string? ConfigurationValueReplaced { get; set; }
    public string EnvironmentVariableName { get; set; } = null!;
    public string? EnvironmentVariableValue { get; set; }
}