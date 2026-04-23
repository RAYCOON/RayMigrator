// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

namespace Raycoon.RayMigrator.Core.Configuration.Enums;

public enum MigrationRunResult : byte
{
    /// <summary>
    /// Invalid ResultId value. ResultId has not been set properly.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Migration process is currently running.
    /// </summary>
    Running = 10,

    /// <summary>
    /// Migration(s) stopped due to error(s).
    /// </summary>
    Error = 90,

    /// <summary>
    /// Migration(s) successfully executed and finished.
    /// </summary>
    Ok = 100,
}
