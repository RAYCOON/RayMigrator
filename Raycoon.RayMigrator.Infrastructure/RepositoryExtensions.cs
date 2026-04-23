// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Database.Common;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core.Extensions;

public static class RepositoryExtensions
{
    public static DalSettings GetDalSettings(this RepositoryOptions repository)
    {
        return new DalSettings
        {
            UseTransaction = false, // Access to repository via repository-template does NOT need a transaction since they are handled within the templates!
            DbCommandTimeoutInSeconds = (int) repository.DbCommandTimeoutInSeconds!,
            MaxRetries = repository.DbCommandMaxRetries ?? 3,
            RetryDelayMs = repository.DbCommandWaitTimeInMsBeforeRetry ?? 500
        };
    }
}