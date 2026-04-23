// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

public class DatabaseLoggingModel
{
    public string DatabaseType { get; set; } = "SqlServer";
    public string ConnectionString { get; set; } = "";
    public string SchemaName { get; set; } = "ray";
    public string TableBaseName { get; set; } = "";
    public string MinimumLevel { get; set; } = "Information";
    public int DbCommandTimeoutInSeconds { get; set; } = 20;
}
