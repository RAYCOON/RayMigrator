// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

/// <summary>
/// Wizard model for a CLI tool configuration.
/// Mirrors CliToolOptions from Core but without validation attributes.
/// </summary>
public class CliToolModel
{
    public string Alias { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string ArgumentTemplate { get; set; } = "";
    public string InputMode { get; set; } = "File";
    public List<string> SuccessExitCodes { get; set; } = new() { "0" };
    public int CliToolTimeoutInSeconds { get; set; } = 120;
}
