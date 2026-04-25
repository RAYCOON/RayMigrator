
using System.Text;
using Microsoft.Extensions.Options;
using Raycoon.RayMigrator.Core.Configuration.Enums;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core.Configuration.Validation;

public class ProductDefaultsPostConfigureOptions : IPostConfigureOptions<RayMigratorOptions>
{
    public void PostConfigure(string? name, RayMigratorOptions options)
    {
        MergeDefaults(options);
    }

    /// <summary>
    /// Merges ProductDefaults into Products, TargetGroups and Targets where explicit values are not set.
    /// This static method can be called directly by AdminOptionsProvider after building RayMigratorOptions from Admin-DB.
    /// </summary>
    /// <param name="options">The RayMigratorOptions to merge defaults into.</param>
    public static void MergeDefaults(RayMigratorOptions options)
    {
        // 1. Validate all default properties:
        //    - ProductDefaults: MigrationErrorAction, MigrationFilesEncoding(string)
        //    - TargetGroupDefaults: TargetMigrationOrder, HashValidationScope
        // 1.1 If valid, copy default values to the corresponding subordinate properties
        // 1.2 If invalid, throw exception indicating the wrong default value in Product or TargetGroup
        // 2. The actual properties are then validated via annotations and must all be set. If not set: hint at missing entry in Defaults.


        if (options.ProductDefaults != null && options.Products != null && options.Products.Any())
        {
            // Check and copy ProductDefault-, TargetGroupDefault and TargetDefault-values to their corresponding configuration-values
            // ONLY SET UNDERLAYING PROPERTY VALUES FROM DEFAULT VALUES WHEN THE DEFAULT VALUES ARE VALID !!!
            foreach (var productOptions in options.Products)
            {
                if (string.IsNullOrWhiteSpace(productOptions.MigrationErrorAction))
                {
                    if (DefaultMigrationErrorActionIsValid(options.ProductDefaults.MigrationErrorAction))
                        productOptions.MigrationErrorAction = options.ProductDefaults.MigrationErrorAction;
                }

                if (string.IsNullOrWhiteSpace(productOptions.RollbackErrorAction))
                {
                    if (DefaultRollbackErrorActionIsValid(options.ProductDefaults.RollbackErrorAction))
                        productOptions.RollbackErrorAction = options.ProductDefaults.RollbackErrorAction;
                }

                if (string.IsNullOrWhiteSpace(productOptions.MigrationFilesEncoding))
                {
                    if (!string.IsNullOrWhiteSpace(options.ProductDefaults.MigrationFilesEncoding) && DefaultMigrationFilesEncodingIsValid(options.ProductDefaults.MigrationFilesEncoding))
                        productOptions.MigrationFilesEncoding = options.ProductDefaults.MigrationFilesEncoding;
                }

                // String-Values, not need to be checked
                if (string.IsNullOrWhiteSpace(productOptions.MigrationFilesExtension)) productOptions.MigrationFilesExtension = options.ProductDefaults.MigrationFilesExtension;
                if (string.IsNullOrWhiteSpace(productOptions.MigrationRollbackFilesPreExtension)) productOptions.MigrationRollbackFilesPreExtension = options.ProductDefaults.MigrationRollbackFilesPreExtension;
                productOptions.RequireRollbackFile ??= options.ProductDefaults.RequireRollbackFile;
                productOptions.StopRollbackOnMissingRollbackFile ??= options.ProductDefaults.StopRollbackOnMissingRollbackFile;

                // UseCliToolAlias: ProductDefaults → Product
                if (string.IsNullOrWhiteSpace(productOptions.UseCliToolAlias)) productOptions.UseCliToolAlias = options.ProductDefaults.UseCliToolAlias;

                // Check and copy TargetGroup-defaults
                if (productOptions.TargetGroups != null && options.ProductDefaults.TargetGroupDefaults != null)
                {
                    foreach (var targetGroupOptions in productOptions.TargetGroups)
                    {
                        if (string.IsNullOrWhiteSpace(targetGroupOptions.TargetMigrationOrder))
                        {
                            if (DefaultTargetMigrationOrderIsValid(options.ProductDefaults.TargetGroupDefaults.TargetMigrationOrder))
                                targetGroupOptions.TargetMigrationOrder = options.ProductDefaults.TargetGroupDefaults.TargetMigrationOrder;
                        }

                        if (string.IsNullOrWhiteSpace(targetGroupOptions.HashValidationScope))
                        {
                            if (DefaultHashValidationScopeIsValid(options.ProductDefaults.TargetGroupDefaults.HashValidationScope))
                                targetGroupOptions.HashValidationScope = options.ProductDefaults.TargetGroupDefaults.HashValidationScope;
                        }

                        targetGroupOptions.StopRollbackOnMissingRollbackFile ??= options.ProductDefaults.TargetGroupDefaults.StopRollbackOnMissingRollbackFile;

                        // UseCliToolAlias: Product → TargetGroup
                        if (string.IsNullOrWhiteSpace(targetGroupOptions.UseCliToolAlias)) targetGroupOptions.UseCliToolAlias = productOptions.UseCliToolAlias;

                        // Check and copy Target-defaults
                        if (targetGroupOptions.Targets != null && options.ProductDefaults.TargetGroupDefaults.TargetDefaults != null)
                        {
                            foreach (var target in targetGroupOptions.Targets)
                            {
                                target.DbCommandTimeoutInSeconds ??= options.ProductDefaults.TargetGroupDefaults.TargetDefaults.DbCommandTimeoutInSeconds;
                                target.DbCommandMaxRetries ??= options.ProductDefaults.TargetGroupDefaults.TargetDefaults.DbCommandMaxRetries;
                                target.DbCommandWaitTimeInMsBeforeRetry ??= options.ProductDefaults.TargetGroupDefaults.TargetDefaults.DbCommandWaitTimeInMsBeforeRetry;

                                // UseCliToolAlias: TargetGroup → Target
                                if (string.IsNullOrWhiteSpace(target.UseCliToolAlias)) target.UseCliToolAlias = targetGroupOptions.UseCliToolAlias;
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="migrationFilesEncoding"></param>
    /// <returns></returns>
    private static bool DefaultMigrationFilesEncodingIsValid(string migrationFilesEncoding)
    {
        try
        {
            _ = Encoding.GetEncoding(migrationFilesEncoding);
            return true;
        }
        catch (Exception ex)
        {
            throw new ConfigurationValidationException(
                $"The ProductDefaults.MigrationFilesEncoding value '{migrationFilesEncoding}' is not a valid encoding name. " +
                $"Some encodings (e.g. 'windows-1252') require System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance) on .NET Core. " +
                $"Please use a valid encoding name like 'UTF-8' or 'iso-8859-1'.", ex);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="migrationErrorAction"></param>
    /// <returns></returns>
    private static bool DefaultMigrationErrorActionIsValid(string? migrationErrorAction)
    {
        return Enum.TryParse(migrationErrorAction, true, out MigrationErrorAction _);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="targetMigrationOrder"></param>
    /// <returns></returns>
    private static bool DefaultRollbackErrorActionIsValid(string? rollbackErrorAction)
    {
        return Enum.TryParse(rollbackErrorAction, true, out RollbackErrorAction _);
    }

    private static bool DefaultTargetMigrationOrderIsValid(string? targetMigrationOrder)
    {
        return Enum.TryParse(targetMigrationOrder, true, out TargetMigrationOrder _);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="HashValidationScope"></param>
    /// <returns></returns>
    private static bool DefaultHashValidationScopeIsValid(string? HashValidationScope)
    {
        return Enum.TryParse(HashValidationScope, true, out HashValidationScope _);
    }
}
