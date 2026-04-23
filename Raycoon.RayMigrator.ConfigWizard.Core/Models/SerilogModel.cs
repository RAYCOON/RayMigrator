// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

public class SerilogModel
{
    public string MinimumLevelDefault { get; set; } = "Information";
    public Dictionary<string, string> MinimumLevelOverrides { get; set; } = new();
    public List<SerilogSinkModel> WriteTo { get; set; } = new();
}

public class SerilogSinkModel
{
    public string Name { get; set; } = "Console";
    public Dictionary<string, string> Args { get; set; } = new();
}
