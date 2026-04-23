// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

public class TargetModel
{
    public string Alias { get; set; } = "";
    public string ConnectionString { get; set; } = "";
    public OverridableValue<int> DbCommandTimeoutInSeconds { get; set; } = new();
    public OverridableValue<int> DbCommandMaxRetries { get; set; } = new();
    public OverridableValue<int> DbCommandWaitTimeInMsBeforeRetry { get; set; } = new();

    /// <summary>Overridable per target. Corresponds to Core's TargetOptions.UseCliToolAlias.</summary>
    public OverridableValue<string> UseCliToolAlias { get; set; } = new();

    /// <summary>
    /// Custom CLI tool parameters for this target, resolved into the ArgumentTemplate.
    /// Corresponds to Core's TargetOptions.CliToolParameters.
    /// </summary>
    public Dictionary<string, string>? CliToolParameters { get; set; }
}
