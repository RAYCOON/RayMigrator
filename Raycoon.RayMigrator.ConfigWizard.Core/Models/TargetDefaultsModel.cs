// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.ConfigWizard.Core.Models;

public class TargetDefaultsModel
{
    public int DbCommandTimeoutInSeconds { get; set; } = 20;
    public int DbCommandMaxRetries { get; set; }
    public int DbCommandWaitTimeInMsBeforeRetry { get; set; } = 250;
}
