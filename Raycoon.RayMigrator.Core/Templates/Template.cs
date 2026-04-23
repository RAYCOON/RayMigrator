// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿namespace Raycoon.RayMigrator.Core.Templates;

public class Template
{
    public TemplateType TemplateType { get; set; } = TemplateType.Undefined;
    public string DatabaseType { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"TemplateType: {TemplateType}, DatabaseType: {DatabaseType}, file: {Filename}";
    }
}
