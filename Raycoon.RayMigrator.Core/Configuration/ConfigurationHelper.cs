using Raycoon.RayMigrator.Core.Templates;

namespace Raycoon.RayMigrator.Core.Configuration;

public class ConfigurationHelper
{
    /// <summary>
    /// Returns a list of TemplateTypes.  
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<TemplateType> GetTemplateTypes()
    {
        return Enum.GetValues(typeof(TemplateType))
            .Cast<TemplateType>()
            .Where(e => e != TemplateType.Undefined);
            //.Select(e => e.ToString()) ==> to get a List<string>
            //.ToList();
    }
}