// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

using Microsoft.JSInterop;

namespace Raycoon.RayMigrator.ConfigWizard.Web.Services;

/// <summary>
/// Provides JS interop for browser file downloads.
/// </summary>
public class FileInteropService
{
    private readonly IJSRuntime _js;

    public FileInteropService(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>
    /// Triggers a browser file download from a byte array.
    /// </summary>
    public async Task DownloadFileAsync(string fileName, string contentType, byte[] content)
    {
        await _js.InvokeVoidAsync("downloadFileFromBytes", fileName, contentType, content);
    }
}
