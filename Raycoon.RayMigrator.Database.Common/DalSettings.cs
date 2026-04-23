// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿namespace Raycoon.RayMigrator.Database.Common;

public class DalSettings : IDalSettings
{
    /// <inheritdoc />
    public bool UseTransaction { get; set; }

    /// <inheritdoc />
    public int DbCommandTimeoutInSeconds { get; set; }

    /// <inheritdoc />
    public int MaxRetries { get; set; } = 3;

    /// <inheritdoc />
    public int RetryDelayMs { get; set; } = 500;
}