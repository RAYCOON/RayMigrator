namespace Raycoon.RayMigrator.Core.Templates;

/// <summary>
/// Parsed result of a template executed as ExecuteScalar. Templates return <c>'code[,code...],message'</c>:
/// one or more integer codes followed by a message. The first code is the <see cref="ResultCode"/>; further
/// codes carry template-specific information, for example whether <c>Repository_CheckCreate</c> created the
/// repository in this run (#27).
/// </summary>
public class TemplateResponse
{
    /// <summary>The first code of the response. Negative values are errors catalogued in <c>TemplateResultCode</c>.</summary>
    public int ResultCode { get; set; }

    /// <summary>All integer codes of the response in order; the first entry equals <see cref="ResultCode"/>. Empty until parsed.</summary>
    public int[] ResultCodes { get; set; } = [];

    /// <summary>The message part of the response; empty when the template returned codes only.</summary>
    public string? ResultMessage { get; set; }

    /// <inheritdoc/>
    public override string ToString()
    {
        string additionalCodes = ResultCodes.Length > 1 ? $" (codes: {string.Join(", ", ResultCodes)})" : string.Empty;
        return $"ResultCode: {ResultCode}{additionalCodes}, ResultMessage: {(string.IsNullOrWhiteSpace(ResultMessage) ? "{NullOrEmpty}" : ResultMessage)}";
    }
}
