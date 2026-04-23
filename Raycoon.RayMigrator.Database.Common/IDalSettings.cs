// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿namespace Raycoon.RayMigrator.Database.Common;

/// <summary>
/// Database-specific settings for DAL execution.
/// </summary>
public interface IDalSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the SQL code should be executed within a transaction.
    /// </summary>
    /// <value>
    /// <c>true</c> if SQL code execution should be wrapped in a transaction; otherwise, <c>false</c>.
    /// </value>
    bool UseTransaction { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed execution time for a SQL command, measured in seconds.
    /// </summary>
    /// <value>
    /// The timeout duration in seconds, after which a running SQL command should be terminated if not completed.
    /// </value>
    int DbCommandTimeoutInSeconds { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for transient database errors.
    /// </summary>
    /// <value>
    /// The number of times to retry a failed operation before giving up. Default is 3.
    /// Set to 0 to disable retries.
    /// </value>
    int MaxRetries { get; set; }

    /// <summary>
    /// Gets or sets the initial delay in milliseconds between retry attempts.
    /// </summary>
    /// <value>
    /// The base delay in milliseconds. Actual delay uses linear backoff: delay * attemptNumber.
    /// Default is 500ms.
    /// </value>
    int RetryDelayMs { get; set; }
}
