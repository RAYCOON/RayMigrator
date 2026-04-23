// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>
/// Captures the user's initial setup choices that drive the ConfigurationScaffolder.
/// </summary>
public class WizardSetupAnswers
{
    public string RepositoryDatabaseType { get; set; } = "SqlServer";
    public List<ProductSetup> Products { get; set; } = new();
    public bool UseDatabaseLogging { get; set; } = true;
    public bool UseCliTools { get; set; }
}

public class ProductSetup
{
    public string Alias { get; set; } = "";
    public List<string> Environments { get; set; } = new();
    public List<TargetGroupSetup> TargetGroups { get; set; } = new();
}

public class TargetGroupSetup
{
    public string Alias { get; set; } = "";
    public string DatabaseType { get; set; } = "SqlServer";
    public List<string> TargetAliases { get; set; } = new();
}
