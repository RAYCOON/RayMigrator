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
        EnsureEnumValuesAreParsable(options);
    }

    /// <summary>
    /// Reads every enum-typed getter once so that an unparseable configuration value fails here, after the
    /// defaults have been merged and before <c>.ValidateDataAnnotations()</c> runs.
    /// </summary>
    /// <remarks>
    /// The <c>*Enum</c> getters throw <see cref="ConfigurationValidationException"/> for a non-empty value that is
    /// not an allowed enum member. <c>DataAnnotationValidateOptions.TryValidateOptions</c> calls
    /// <c>PropertyInfo.GetValue</c> on every public instance property of the options graph (not only on
    /// attributed ones) while looking for <c>[ValidateObjectMembers]</c> / <c>[ValidateEnumeratedItems]</c>, so
    /// without this probe the first invalid value would escape as a <see cref="System.Reflection.TargetInvocationException"/>
    /// and the remaining ones would go unreported. Probing the raw strings here reports all invalid enum values
    /// in one message with their location, in the wording of <c>RayEnumAttribute</c>. Trade-off: other
    /// data-annotation and rule-catalog findings are only reported once the enum values are fixed.
    /// </remarks>
    /// <exception cref="ConfigurationValidationException">At least one enum-typed value cannot be parsed.</exception>
    public static void EnsureEnumValuesAreParsable(RayMigratorOptions options)
    {
        var errors = new List<string>();

        if (options.ProductDefaults != null)
        {
            Probe<MigrationErrorAction>(errors, "ProductDefaults", options.ProductDefaults.MigrationErrorAction, nameof(ProductDefaultOptions.MigrationErrorAction));
            Probe<RollbackErrorAction>(errors, "ProductDefaults", options.ProductDefaults.RollbackErrorAction, nameof(ProductDefaultOptions.RollbackErrorAction));

            if (options.ProductDefaults.TargetGroupDefaults != null)
            {
                Probe<TargetMigrationOrder>(errors, "ProductDefaults.TargetGroupDefaults", options.ProductDefaults.TargetGroupDefaults.TargetMigrationOrder, nameof(TargetGroupDefaultOptions.TargetMigrationOrder));
                Probe<HashValidationScope>(errors, "ProductDefaults.TargetGroupDefaults", options.ProductDefaults.TargetGroupDefaults.HashValidationScope, nameof(TargetGroupDefaultOptions.HashValidationScope));
            }
        }

        if (options.Products != null)
        {
            for (var p = 0; p < options.Products.Count; p++)
            {
                var product = options.Products[p];
                var productPath = $"Products[{p}] (Alias '{product.Alias}')";
                Probe<MigrationErrorAction>(errors, productPath, product.MigrationErrorAction, nameof(ProductOptions.MigrationErrorAction));
                Probe<RollbackErrorAction>(errors, productPath, product.RollbackErrorAction, nameof(ProductOptions.RollbackErrorAction));

                if (product.TargetGroups == null) continue;

                for (var t = 0; t < product.TargetGroups.Count; t++)
                {
                    var targetGroup = product.TargetGroups[t];
                    var targetGroupPath = $"Products[{p}].TargetGroups[{t}] (Alias '{targetGroup.Alias}')";
                    Probe<TargetMigrationOrder>(errors, targetGroupPath, targetGroup.TargetMigrationOrder, nameof(TargetGroupOptions.TargetMigrationOrder));
                    Probe<HashValidationScope>(errors, targetGroupPath, targetGroup.HashValidationScope, nameof(TargetGroupOptions.HashValidationScope));
                }
            }
        }

        if (options.CliTools != null)
        {
            for (var c = 0; c < options.CliTools.Count; c++)
            {
                var cliTool = options.CliTools[c];
                Probe<CliToolInputMode>(errors, $"CliTools[{c}] (Alias '{cliTool.Alias}')", cliTool.InputMode, nameof(CliToolOptions.InputMode));
            }
        }

        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(
                $"{errors.Count} enum-typed configuration value(s) could not be parsed:" +
                string.Concat(errors.Select(e => Environment.NewLine + "  - " + e)));
        }
    }

    /// <summary>
    /// Applies the same rule as the <c>*Enum</c> getters to a raw string value. Null / whitespace is the
    /// "not set" sentinel and is never an error here; a required-but-missing value is reported by
    /// <c>RayEnumAttribute</c> in the data-annotation step.
    /// </summary>
    private static void Probe<TEnum>(List<string> errors, string path, string? raw, string propertyName) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw)) return;

        if (!OptionsEnumParser.TryParse<TEnum>(raw, propertyName, out _, out var error))
        {
            errors.Add($"{path}: {error}");
        }
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
        return OptionsEnumParser.TryParse<MigrationErrorAction>(migrationErrorAction, nameof(ProductDefaultOptions.MigrationErrorAction), out _, out _);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="targetMigrationOrder"></param>
    /// <returns></returns>
    private static bool DefaultRollbackErrorActionIsValid(string? rollbackErrorAction)
    {
        return OptionsEnumParser.TryParse<RollbackErrorAction>(rollbackErrorAction, nameof(ProductDefaultOptions.RollbackErrorAction), out _, out _);
    }

    private static bool DefaultTargetMigrationOrderIsValid(string? targetMigrationOrder)
    {
        return OptionsEnumParser.TryParse<TargetMigrationOrder>(targetMigrationOrder, nameof(TargetGroupDefaultOptions.TargetMigrationOrder), out _, out _);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="HashValidationScope"></param>
    /// <returns></returns>
    private static bool DefaultHashValidationScopeIsValid(string? HashValidationScope)
    {
        return OptionsEnumParser.TryParse<HashValidationScope>(HashValidationScope, nameof(TargetGroupDefaultOptions.HashValidationScope), out _, out _);
    }
}
