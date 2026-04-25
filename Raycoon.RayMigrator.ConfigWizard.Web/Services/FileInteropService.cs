
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
