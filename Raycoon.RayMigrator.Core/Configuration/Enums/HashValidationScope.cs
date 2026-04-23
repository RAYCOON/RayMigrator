// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿namespace Raycoon.RayMigrator.Core.Configuration.Enums;

/// <summary>
/// Enum for RayMigratorOptions
/// </summary>
public enum HashValidationScope : byte
{
    Undefined = 0,
    File = 1,
    SqlBlocks = 2,
    Disabled = 3,
}