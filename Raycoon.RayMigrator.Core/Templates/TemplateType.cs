// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿namespace Raycoon.RayMigrator.Core.Templates;

public enum TemplateType
{
    Undefined,

    DatabaseLogging_CheckCreate,
    DatabaseLogging_Insert,

    Repository_CheckCreate,
    Repository_Drop,

    Repository_MigrationRun_Insert,
    Repository_MigrationRun_Update,
    Repository_MigrationRun_SelectOrphaned,
    Repository_MigrationRun_FixOrphaned,
    Repository_Migration_FixOrphaned,

    Repository_Product_CheckInsert,
    Repository_Environment_CheckInsert,

    // Recovery and state tracking templates
    Repository_Migration_Insert,
    Repository_Migration_Update,
    Repository_Migration_UpdateHash,
    Repository_Migration_UpdateRollback,
    Repository_Migration_Select,
    Repository_Migration_GetInterrupted,
    Repository_MigrationRun_Select
}
