// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿using System.Text.RegularExpressions;

namespace Raycoon.RayMigrator.Core.Configuration;

public static class ConfigurationConstants
{
    /// <summary>
    /// For appsettings.json use.
    /// </summary>
    public const string EnvironmentVariablePrefix = "{ENV:";

    /// <summary>
    /// For appsettings.json use.
    /// </summary>
    public const string EnvironmentVariablePostfix = "}";
    
    public static readonly Regex EnvironmentVariableRegex = new (@"\" + EnvironmentVariablePrefix + @"(.*?)\" + ConfigurationVariablePostfix, RegexOptions.Compiled);

    
    /// <summary>
    /// For DataAccessLayer-Template use.
    /// </summary>
    public const string ConfigurationVariablePrefix = "{CFG:";
    
    public static readonly Regex ConfigurationVariableRegex = new (@"\" + ConfigurationVariablePrefix + @"([A-Za-z0-9_]+)\" + ConfigurationVariablePostfix, RegexOptions.Compiled);

    /// <summary>
    /// For DataAccessLayer-Template use.
    /// </summary>
    public const string ConfigurationVariablePostfix = "}";
    
    public const char ConfigurationValueDelimiter = ',';
    
    public const string NotRevealSensitiveDataString = "*** HIDDEN ***";
    
    /// <summary>
    /// List of {CFG: - configuration property names that are allowed to be replaced when defined within Templates
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedTemplateVariableNameReplacements = new[] { "SchemaName", "TableBaseName" };
    
    public const string DotNetEnvironmentVariableName = "DOTNET_ENVIRONMENT";

    public const string DatabaseAccessLayersRootDirectory = "DataAccessLayers";
}