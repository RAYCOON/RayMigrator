// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿using Microsoft.Extensions.Logging;

namespace Raycoon.RayMigrator.Core.Configuration.Enums;

public class MigrationEvent
{
    public static readonly EventId UnspecifiedEvent = new EventId(0, "UnspecifiedEvent");
    
    // Application Startup
    public static readonly EventId CommandLineParsing = new EventId(10, "CommandLineParsing");
    public static readonly EventId EnvironmentVariableReplacement = new EventId(20, "EnvironmentVariableReplacement");
    public static readonly EventId CreateDatabaseLogger = new EventId(31, "CreateDatabaseLogger");
    public static readonly EventId ValidateRayMigratorOptions = new EventId(40, "ValidateRayMigratorOptions");
    public static readonly EventId CreateApplicationHost = new EventId(50, "CreateApplicationHost");
    public static readonly EventId InitializeDalSpecificProperties = new EventId(60, "InitializeDalSpecificProperties");
    public static readonly EventId ValidateConnectionStrings = new EventId(70, "ValidateConnectionStrings");
    public static readonly EventId RayMigratorServiceStart = new EventId(80, "RayMigratorServiceStart");
    
    // Template Execution - Repository Operations
    public static readonly EventId TemplateExecutionRepositoryCheckCreate = new EventId(100, "TemplateExecutionRepositoryCheckCreate");
    public static readonly EventId TemplateExecutionRepositoryMigrationRunInsert = new EventId(110, "TemplateExecutionMigrationRunInsert");
    public static readonly EventId TemplateExecutionRepositoryMigrationRunUpdate = new EventId(111, "TemplateExecutionMigrationRunUpdate");
    public static readonly EventId TemplateExecutionRepositoryMigrationRunSelectOrphaned = new EventId(112, "TemplateExecutionMigrationRunSelectOrphaned");
    public static readonly EventId TemplateExecutionRepositoryMigrationRunFixOrphaned = new EventId(113, "TemplateExecutionMigrationRunFixOrphaned");
    public static readonly EventId TemplateExecutionRepositoryMigrationFixOrphaned = new EventId(114, "TemplateExecutionMigrationFixOrphaned");
    public static readonly EventId TemplateExecutionRepositoryProductCheckInsert = new EventId(120, "TemplateExecutionProductCheckInsert");
    public static readonly EventId TemplateExecutionRepositoryEnvironmentCheckInsert = new EventId(121, "TemplateExecutionEnvironmentCheckInsert");

    // Template Execution - Migration Operations
    public static readonly EventId TemplateExecutionRepositoryMigrationInsert = new EventId(130, "TemplateExecutionMigrationInsert");
    public static readonly EventId TemplateExecutionRepositoryMigrationUpdate = new EventId(131, "TemplateExecutionMigrationUpdate");
    public static readonly EventId TemplateExecutionRepositoryMigrationGetInterrupted = new EventId(132, "TemplateExecutionMigrationGetInterrupted");
    public static readonly EventId TemplateExecutionRepositoryMigrationUpdateRollback = new EventId(133, "TemplateExecutionMigrationUpdateRollback");
    public static readonly EventId TemplateExecutionRepositoryMigrationSelect = new EventId(134, "TemplateExecutionMigrationSelect");
    public static readonly EventId TemplateExecutionRepositoryMigrationUpdateHash = new EventId(135, "TemplateExecutionMigrationUpdateHash");
    public static readonly EventId TemplateExecutionRepositoryMigrationRunSelect = new EventId(136, "TemplateExecutionMigrationRunSelect");

    // Application Shutdown
    public static readonly EventId RayMigratorServiceShutdown = new EventId(1000, "RayMigratorServiceShutdown");
    
}