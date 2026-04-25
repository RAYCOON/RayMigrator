namespace Raycoon.RayMigrator.Core.Templates;

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
