// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

public class RepositoryModel
{
    public string DatabaseType { get; set; } = "SqlServer";
    public string ConnectionString { get; set; } = "";
    public string SchemaName { get; set; } = "ray";
    public string TableBaseName { get; set; } = "";
    public int DbCommandTimeoutInSeconds { get; set; } = 60;
    public int DbCommandMaxRetries { get; set; } = 100;
    public int DbCommandWaitTimeInMsBeforeRetry { get; set; } = 250;
}
