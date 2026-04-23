// Copyright (c) 2026 RAYCOON.com GmbH
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License v3.
//
// See the LICENSE file for details.

﻿using System.Globalization;

namespace Raycoon.RayMigrator.Core;

public class CultureDependentSorting
{
    public void Sort(string rootDirectory, string searchPattern, bool ignoreCase, CultureInfo? cultureInfo = null)
    {
        // Culture can be the current OS culture, for example
        cultureInfo ??= CultureInfo.CurrentCulture;
        // Or explicitly, e.g.: CultureInfo.GetCultureInfo("de-DE") // see https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-lcid/a9eac961-e77d-41a6-90a5-ce1a8b0cdb9c

        // Retrieve files and sort them culture-dependently (by directory + filename)
        List<FileInfo> files = GetAndSortFilesRecursive(
            rootDirectory,
            searchPattern,
            ignoreCase,
            cultureInfo
        );

        // Output
        Console.WriteLine($"Number of files found ({files.Count}):");
        foreach (var fileInfo in files)
        {
            Console.WriteLine(fileInfo.FullName);
        }

        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }

    /// <summary>
    /// Recursively searches for all files in the given root directory matching the search pattern.
    /// Sorts them by (relative) subdirectory and filename using the specified culture.
    /// </summary>
    /// <param name="rootDirectory">Base directory from which the search starts.</param>
    /// <param name="searchPattern">File search pattern (e.g. "*.sql*").</param>
    /// <param name="ignoreCase">If true, case is ignored during comparison.</param>
    /// <param name="culture">The culture to use for sorting (e.g. "de-DE").</param>
    /// <returns>List of all matching files in sorted order.</returns>
    public static List<FileInfo> GetAndSortFilesRecursive(
        string rootDirectory,
        string searchPattern,
        bool ignoreCase,
        CultureInfo culture)
    {
        // 1) Recursively retrieve all matching files
        var allFilePaths = Directory.EnumerateFiles(
            rootDirectory,
            searchPattern,
            SearchOption.AllDirectories);

        // 2) Create FileInfo objects
        var allFileInfos = allFilePaths.Select(path => new FileInfo(path));

        // 3) Create a culture-dependent StringComparer that optionally ignores case
        var stringComparer = StringComparer.Create(culture, ignoreCase);

        // 4) Sort:
        //    - First by relative directory (relative to rootDirectory)
        //    - Then by filename
        var sortedFiles = allFileInfos
            .OrderBy(file => Path.GetRelativePath(rootDirectory, file.DirectoryName ?? string.Empty), stringComparer)
            .ThenBy(file => file.Name, stringComparer)
            .ToList();

        return sortedFiles;
    }
}