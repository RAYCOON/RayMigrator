// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿namespace Raycoon.RayMigrator.Core.Templates;

public class TemplateResponse
{
    public int ResultCode { get; set; }
    public string? ResultMessage { get; set; }
    
    public override string ToString()
    {
        return $"ResultCode: {ResultCode}, ResultMessage: {(string.IsNullOrWhiteSpace(ResultMessage) ? "{NullOrEmpty}" : ResultMessage)}";
    }
}