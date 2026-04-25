using System.Data.Common;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Raycoon.RayMigrator.Core.Configuration.Options;
using Raycoon.RayMigrator.Shared.Exceptions;

namespace Raycoon.RayMigrator.Core.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Replaces {CFG:Placeholders} within an arbitrary string from values of a property class for all property names that correspond.
    /// </summary>
    /// <param name="propertyClass"></param>
    /// <param name="stringToReplace"></param>
    /// <param name="replacementRegex"></param>
    /// <param name="propertyNames"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static string ReplacePlaceholdersFromPropertyClass<T>(this string stringToReplace, T propertyClass, Regex replacementRegex, IReadOnlyList<string>? propertyNames = null) // replacementRegex = Constants.ConfigurationVariableRegex
    {
        // Replace each placeholder with the corresponding property value, if the name is in propertyNames
        var result = replacementRegex.Replace(stringToReplace, match =>
        {
            // Get the property name from the placeholder
            var propertyName = match.Groups[1].Value;

            // Check if the property name is in the list of names to replace
            if (propertyNames == null || propertyNames.Contains(propertyName, StringComparer.OrdinalIgnoreCase))
            {
                // Find the property in the class that matches the placeholder name
                var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (property != null)
                {
                    // Read the property value and convert it to string
                    var propertyValue = property.GetValue(propertyClass)?.ToString();

                    // Replace the placeholder with the property value; if null, keep the placeholder unchanged
                    return propertyValue ?? match.Value;
                }
            }

            // If no matching property was found or the name is not in the list, keep the placeholder unchanged
            return match.Value;
        });

        return result;
    }

    /// <summary>
    /// Validates if the provided connection string is valid.
    /// </summary>
    /// <param name="connectionString">The ConnectionString to be validated.</param>
    /// <param name="exceptionMessage">The Exception-Message when trying to create a DbConnnectionStringBuilder.</param>
    public static bool IsValidConnectionString(this string connectionString, out string? exceptionMessage)
    {
        try
        {
            _ = new DbConnectionStringBuilder { ConnectionString = connectionString };
            exceptionMessage = null;
            return true;
        }
        catch(Exception ex)
        {
            exceptionMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Tries to get the database name from a given ConnectionString.
    /// </summary>
    /// <param name="connectionString">The ConnectionString.</param>
    /// <param name="databaseName"></param>
    /// <returns></returns>
    public static bool TryGetDatabaseNameFromConnectionString(this string connectionString, out string? databaseName)
    {
        var builder = new DbConnectionStringBuilder();
        try
        {
            builder.ConnectionString = connectionString;
        }
        catch
        {
            databaseName = null;
            return false;
        }

        object? value;

        if (builder.TryGetValue("Initial Catalog", out value))
        {
            databaseName = value.ToString();
            if (!string.IsNullOrEmpty(databaseName)) return true;
        }
        
        if (builder.TryGetValue("Database", out value))
        {
            databaseName = value.ToString();
            if (!string.IsNullOrEmpty(databaseName)) return true;
        }

        if (builder.TryGetValue("Data Source", out value))
        {
            databaseName = value.ToString();
            
            if (!string.IsNullOrEmpty(databaseName))
            {
                // SQLite may contain path as Data Source
                if (Path.IsPathFullyQualified(databaseName))
                {
                    var databaseNameTmp = Path.GetFileNameWithoutExtension(databaseName);
                    databaseName = databaseNameTmp;
                    return true;
                }
            
                // DatabaseName already set
                return true;
            }
        }

        databaseName = null;
        return false;
    }
    
    /// <summary>
    /// Generates a SHA256-hash string from an input-string.
    /// </summary>
    /// <param name="inputString">The input string.</param>
    /// <returns>The SHA-256 value of the input string.</returns>
    public static string GenerateSha256(this string inputString)
    {
        using (SHA256 sha256Hash = SHA256.Create())
        {
            // ComputeHash - returns a byte Array
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(inputString));

            // Comvert byte-Array into a string
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }


    /// <summary>
    /// Extracts the first path segment from a given string that contains path sections separated by '\' or '/'.
    /// </summary>
    /// <param name="path">The input string containing the full path.</param>
    /// <returns>The first path segment as a string, or an empty string if the input is null, empty, or contains no path segments.</returns>
    public static string[] GetPathSegments(this string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new string[]{string.Empty};
        }

        // Consider path separators: both '\' and '/'
        char[] separators = new char[] { '\\', '/' };

        // Split the path based on the separators and take the first segment
        var segments = path.Split(separators, StringSplitOptions.RemoveEmptyEntries);
        return segments;
    }
    
    /// <summary>
    /// Extracts the first path segment from a given string that contains path sections separated by '\' or '/'.
    /// </summary>
    /// <param name="path">The input string containing the full path.</param>
    /// <returns>The first path segment as a string, or an empty string if the input is null, empty, or contains no path segments.</returns>
    public static string GetFirstPathSegment(this string path)
    {
        string[] pathSegments = GetPathSegments(path);
        string firstSegment = pathSegments.Length > 0 ? pathSegments[0] : string.Empty;
        return firstSegment;
    }

    /// <summary>
    /// Returns the relative path from a complete path and a base-path.
    /// </summary>
    /// <param name="completePath">The complete path like 'D:\Migrations\SubDir\MyFile.sql'.</param>
    /// <param name="basePath">The base path like 'D:\Migrations'.</param>
    /// <returns>The relative path like 'SubDir'</returns>
    public static string GetRelativePath(this string completePath, string basePath)
    {
        string relativePath = completePath.Replace(basePath, string.Empty);
        if (relativePath.StartsWith(@"\") || relativePath.StartsWith(@"/"))
        {
            relativePath = relativePath.Substring(1);
        }

        string relativePathSysetem = Path.GetRelativePath(basePath, completePath);
        return relativePath;
    }
    /// <summary>
    /// Returns the parent path from a given path.
    /// </summary>
    /// <param name="path">The path like 'D:\Migrations\SubDir'.</param>
    /// <returns>The parent path like 'D:\Migrations'</returns>
    public static string GetParentPath(this string path)
    {
        string parentPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return parentPath;
        }

        if (path.Contains(Path.DirectorySeparatorChar))
        {
            parentPath = path.Substring(0, path.LastIndexOf(Path.DirectorySeparatorChar));
        }
        else if (path.Contains(Path.AltDirectorySeparatorChar))
        {
            parentPath = path.Substring(0, path.LastIndexOf(Path.AltDirectorySeparatorChar));
        }

        return parentPath;
    }

    /// <summary>
    /// Gets the ReleaseVersion and TargetGroupAlias from the directory of a file and it's filename. 
    /// </summary>
    /// <exception cref="ConfigurationValidationException"></exception>
    public static void GetReleaseVersionAndTargetGroupAlias(this string filenameWithRelativePath, RayMigratorOptions options, string productAlias, out string releaseVersion, out string targetGroupAlias)
    {
        var pathSegments = filenameWithRelativePath.GetPathSegments();
        List<string> targetGroupOptionsAliases = options.Products!.First(p => p.Alias == productAlias).TargetGroups!.Select(mg => mg.Alias!).ToList();
                
        // Extract ReleaseVersion
        if (pathSegments.Length > 0)
        {
            releaseVersion = pathSegments[0];
        }
        else
        {
            throw new ConfigurationValidationException($"Could not determine release version from path [{filenameWithRelativePath}]");
        }
        
        // Extract TargetGroupAlias
        targetGroupAlias = string.Empty;
        if (pathSegments.Length > 1)
        {
            if(targetGroupOptionsAliases.Contains(pathSegments[1]))
            {
                targetGroupAlias = pathSegments[1];
            }
            else
            {
                string[] fileNameSegments = Path.GetFileName(filenameWithRelativePath).Split('.');
                if (fileNameSegments.Length > 2)
                {
                    string targetGroupAliasTest = fileNameSegments[fileNameSegments.Length - 2];    
                    if(targetGroupOptionsAliases.Contains(targetGroupAliasTest))
                    {
                        targetGroupAlias = targetGroupAliasTest;
                    }
                }
            }
        }

        // Flat layout: auto-assign when exactly one TargetGroup is configured
        // and the path has at least a release directory + filename (pathSegments >= 2)
        if (string.IsNullOrWhiteSpace(targetGroupAlias) && targetGroupOptionsAliases.Count == 1 && pathSegments.Length >= 2)
        {
            targetGroupAlias = targetGroupOptionsAliases[0];
        }

        if (string.IsNullOrWhiteSpace(targetGroupAlias))
        {
            throw new ConfigurationValidationException($"Could not determine TargetGroup-Alias from path [{filenameWithRelativePath}]");
        }
    }

    /// <summary>
    /// Normalizes path separators according to the current platform.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>The normalized path.</returns>
    public static string NormalizePath(this string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Replace all separators with the platform-specific separator
        path = path.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        // Remove consecutive separators
        while (path.Contains($"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}"))
        {
            path = path.Replace(
                $"{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}", 
                Path.DirectorySeparatorChar.ToString());
        }

        return path;    
    }
}
