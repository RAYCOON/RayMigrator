using System.Text;
using Microsoft.Extensions.Configuration;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Core.Configuration;

namespace Raycoon.RayMigrator.Core.Extensions;

public static class RayMigratorOptionsExtensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="config"></param>
    /// <param name="revealSensitiveData"></param>
    /// <returns></returns>
    public static string ToDetailString(this IConfigurationSection config, bool revealSensitiveData)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine($"Environment:                          {config["Environment"]}");
        sb.AppendLine($"RunMode:                              {config["RunMode"]}");
        sb.AppendLine($"MigrationErrorAction:                 {config["MigrationErrorAction"]}");
        sb.AppendLine($"MigrationFilesRootDirectory:          {(revealSensitiveData ? config["MigrationFilesRootDirectory"] : ConfigurationConstants.NotRevealSensitiveDataString)}");
        sb.AppendLine($"MigrationFilesExtension:              {config["MigrationFilesExtension"]}");
        sb.AppendLine($"MigrationRollbackFilesPreExtension:   {config["MigrationRollbackFilesPreExtension"]}");
        sb.AppendLine($"MigrationFilesEncoding:               {config["MigrationFilesEncoding"]}");
        sb.AppendLine($"DatabaseAccessLayersRootDirectory:    {(revealSensitiveData ? config["DatabaseAccessLayersRootDirectory"] : ConfigurationConstants.NotRevealSensitiveDataString)}");
        sb.AppendLine($"ShowStartupInfo:                      {config["ShowStartupInfo"]}");
        sb.AppendLine($"RevealSensitiveData:                  {config["RevealSensitiveData"]}");
        
        var repository = config.GetSection("Repository");
        sb.AppendLine();  
        sb.AppendLine($"Repository:");
        sb.AppendLine($"  DatabaseType:                       {repository["DatabaseType"]}");
        sb.AppendLine($"  ConnectionString:                   {(revealSensitiveData ? repository["ConnectionString"] : ConfigurationConstants.NotRevealSensitiveDataString)}");
        sb.AppendLine($"  SchemaName:                         {(revealSensitiveData ? repository["SchemaName"] : ConfigurationConstants.NotRevealSensitiveDataString)}");
        sb.AppendLine($"  TableBaseName:                      {(revealSensitiveData ? repository["TableBaseName"] : ConfigurationConstants.NotRevealSensitiveDataString)}");
        sb.AppendLine($"  DbCommandTimeoutInSeconds:          {repository["DbCommandTimeoutInSeconds"]}");

        var targetGroupsSection = config.GetSection("TargetGroups");
        if (targetGroupsSection.Exists())
        {
            foreach (var targetGroup in targetGroupsSection.GetChildren())
            {
                sb.AppendLine();
                sb.AppendLine($"TargetGroup:                          {targetGroup["Alias"]}");
                sb.AppendLine($"  DatabaseType:                       {targetGroup["DatabaseType"]}");
                sb.AppendLine($"  TargetMigrationOrder:                     {targetGroup["TargetMigrationOrder"]}");
                sb.AppendLine($"  HashValidationScope: {targetGroup["HashValidationScope"]}");


                var targetsSection = targetGroup.GetSection("Targets");
                if (targetsSection.Exists())
                {
                    foreach (var target in targetsSection.GetChildren())
                    {
                        sb.AppendLine();  
                        sb.AppendLine($"Target:                               {target["Alias"]}");
                        sb.AppendLine($"  ConnectionString:                   {(revealSensitiveData ? target["ConnectionString"] : ConfigurationConstants.NotRevealSensitiveDataString)}");
                        sb.AppendLine($"  DbCommandTimeoutInSeconds:          {target["DbCommandTimeoutInSeconds"]}");
                        sb.AppendLine($"  DbCommandMaxRetries:                {target["DbCommandMaxRetries"]}");
                        sb.AppendLine($"  DbCommandWaitTimeInMsBeforeRetry:   {target["DbCommandWaitTimeInMsBeforeRetry"]}");
                    }
                }
            }
        }

        return sb.ToString();
    }
}