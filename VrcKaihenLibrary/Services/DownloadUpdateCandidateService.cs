using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VrcKaihenLibrary.Services;

public static partial class DownloadUpdateCandidateService
{
    public static IReadOnlySet<string> FindUnityPackageCandidates(string rootPath, IEnumerable<string> unityPackagePaths)
        => FindUnityPackageVersionParents(rootPath, unityPackagePaths).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string> FindUnityPackageVersionParents(
        string rootPath, IEnumerable<string> unityPackagePaths)
    {
        var paths = unityPackagePaths.ToList();
        var duplicateFileNames = paths
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var packages = paths
            .Select(path => new PackageVersionInfo(path, ReadPackageIdentity(rootPath, path,
                duplicateFileNames.Contains(Path.GetFileName(path)))))
            .ToList();
        var parents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var family in packages
            .Where(x => x.Identity.Family.Length >= 4)
            .GroupBy(x => x.Identity.Family, StringComparer.OrdinalIgnoreCase))
        {
            var newest = family
                .Where(x => x.Identity.Version is not null)
                .OrderByDescending(x => x.Identity.Version)
                .ThenByDescending(x => File.GetLastWriteTimeUtc(x.Path))
                .FirstOrDefault();
            if (newest is null) continue;

            foreach (var older in family.Where(x => !ReferenceEquals(x, newest)
                && (x.Identity.Version is null || x.Identity.Version < newest.Identity.Version)))
                parents[older.Path] = newest.Path;
        }

        AddDuplicateDownloadFolderParents(rootPath, packages, parents);

        return parents;
    }

    public static bool IsDuplicateDownloadFolderPair(string rootPath, string firstPath, string secondPath)
    {
        var first = ReadTopLevelDownloadFolder(rootPath, firstPath);
        var second = ReadTopLevelDownloadFolder(rootPath, secondPath);
        return first is not null
            && second is not null
            && first.BaseName.Equals(second.BaseName, StringComparison.OrdinalIgnoreCase)
            && first.CopyNumber != second.CopyNumber;
    }

    private static void AddDuplicateDownloadFolderParents(
        string rootPath,
        IReadOnlyList<PackageVersionInfo> packages,
        IDictionary<string, string> parents)
    {
        var folderPackages = packages
            .Select(package => new
            {
                Package = package,
                Folder = ReadTopLevelDownloadFolder(rootPath, package.Path)
            })
            .Where(x => x.Folder is not null)
            .Select(x => new { x.Package, Folder = x.Folder! })
            .ToList();

        foreach (var duplicate in folderPackages.Where(x => x.Folder.CopyNumber >= 2))
        {
            if (parents.ContainsKey(duplicate.Package.Path)) continue;

            var original = folderPackages
                .Where(candidate => !candidate.Package.Path.Equals(duplicate.Package.Path, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(candidate.Package.Path).Equals(
                        Path.GetFileName(duplicate.Package.Path), StringComparison.OrdinalIgnoreCase)
                    && candidate.Folder.BaseName.Equals(
                        duplicate.Folder.BaseName, StringComparison.OrdinalIgnoreCase)
                    && candidate.Folder.CopyNumber < duplicate.Folder.CopyNumber)
                .OrderBy(candidate => candidate.Folder.CopyNumber)
                .ThenByDescending(candidate => File.GetLastWriteTimeUtc(candidate.Package.Path))
                .FirstOrDefault();

            if (original is not null) parents[duplicate.Package.Path] = original.Package.Path;
        }
    }

    private static DownloadFolderIdentity? ReadTopLevelDownloadFolder(string rootPath, string packagePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, packagePath);
        var components = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        if (components.Length < 2) return null;

        var folderName = components[0].Normalize(NormalizationForm.FormKC).Trim();
        var match = DuplicateDownloadFolderRegex().Match(folderName);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var copyNumber))
            return new DownloadFolderIdentity(folderName, 1);

        var baseName = folderName[..match.Index].TrimEnd();
        return string.IsNullOrWhiteSpace(baseName)
            ? null
            : new DownloadFolderIdentity(baseName, copyNumber + 1);
    }

    private static VersionedName ReadPackageIdentity(
        string rootPath, string packagePath, bool useImmediateParentVersion)
    {
        var relativePath = Path.GetRelativePath(rootPath, packagePath);
        var pathComponents = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var components = pathComponents
            .Select((component, index) => index == pathComponents.Length - 1
                ? Path.GetFileNameWithoutExtension(component)
                : component)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => CreateVersionedName(x!))
            .ToList();
        var fileName = components[^1];
        if (fileName.Version is not null) return fileName;
        for (var index = components.Count - 2; index >= 0; index--)
        {
            var directory = components[index];
            if (directory.Version is null) continue;
            if (directory.Family.Equals(fileName.Family, StringComparison.OrdinalIgnoreCase)
                || directory.Family.Contains(fileName.Family, StringComparison.OrdinalIgnoreCase)
                || fileName.Family.Contains(directory.Family, StringComparison.OrdinalIgnoreCase))
                return new VersionedName(fileName.Family, directory.Version);
        }
        if (useImmediateParentVersion && components.Count >= 2 && components[^2].Version is not null)
            return new VersionedName(fileName.Family, components[^2].Version);
        return fileName;
    }

    private static VersionedName CreateVersionedName(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC);
        var withoutVersion = VersionSuffixRegex().Replace(normalized, string.Empty);
        return new VersionedName(Normalize(withoutVersion), TryReadVersion(normalized));
    }

    private static Version? TryReadVersion(string value)
    {
        var match = VersionTokenRegex().Match(value);
        if (!match.Success) return null;
        var versionText = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        var parts = versionText.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(x => !int.TryParse(x, out _))) return null;
        var numbers = parts.Select(int.Parse).Concat(Enumerable.Repeat(0, 4)).Take(4).ToArray();
        return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
    }

    private static string Normalize(string value) => Regex.Replace(
        value, @"[^\p{L}\p{N}]", string.Empty, RegexOptions.CultureInvariant).ToLowerInvariant();

    private sealed record PackageVersionInfo(string Path, VersionedName Identity);
    private sealed record VersionedName(string Family, Version? Version);
    private sealed record DownloadFolderIdentity(string BaseName, int CopyNumber);

    [GeneratedRegex(@"\s*\((\d+)\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex DuplicateDownloadFolderRegex();

    [GeneratedRegex(@"(?:^|[\s_.-])(?:(?:v(?:er(?:sion)?)?\.?\s*)\d+(?:[._-]\d+){0,3}|\d+(?:[._-]\d+){1,3})[a-z]?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionSuffixRegex();

    [GeneratedRegex(@"(?:^|[\s_.-])(?:(?:v(?:er(?:sion)?)?\.?\s*)(\d+(?:[._-]\d+){0,3})|(\d+(?:[._-]\d+){1,3}))(?:[a-z])?(?:$|[\s_.-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.RightToLeft)]
    private static partial Regex VersionTokenRegex();
}
