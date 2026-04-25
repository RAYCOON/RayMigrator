namespace Raycoon.RayMigrator.Core.Templates;

public class TemplateResponse
{
    public int ResultCode { get; set; }
    public string? ResultMessage { get; set; }
    
    public override string ToString()
    {
        return $"ResultCode: {ResultCode}, ResultMessage: {(string.IsNullOrWhiteSpace(ResultMessage) ? "{NullOrEmpty}" : ResultMessage)}";
    }
}