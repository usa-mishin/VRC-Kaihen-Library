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
    {
        var packages = unityPackagePaths
            .Select(path => new PackageVersionInfo(path, ReadPackageIdentity(rootPath, path)))
            .ToList();
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var current in packages)
        {
            foreach (var other in packages)
            {
                if (ReferenceEquals(current, other)) continue;
                if (current.Identity.Family.Length < 4
                    || !current.Identity.Family.Equals(other.Identity.Family, StringComparison.OrdinalIgnoreCase)
                    || other.Identity.Version is null)
                    continue;
                if (current.Identity.Version is null || current.Identity.Version < other.Identity.Version)
                    candidates.Add(current.Path);
                if (candidates.Contains(current.Path)) break;
            }
        }

        return candidates;
    }

    private static VersionedName ReadPackageIdentity(string rootPath, string packagePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, packagePath);
        var components = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Select(Path.GetFileNameWithoutExtension)
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

    [GeneratedRegex(@"(?:^|[\s_.-])(?:(?:v(?:er(?:sion)?)?\.?\s*)\d+(?:[._-]\d+){0,3}|\d+(?:[._-]\d+){1,3})[a-z]?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionSuffixRegex();

    [GeneratedRegex(@"(?:^|[\s_.-])(?:(?:v(?:er(?:sion)?)?\.?\s*)(\d+(?:[._-]\d+){0,3})|(\d+(?:[._-]\d+){1,3}))(?:[a-z])?(?:$|[\s_.-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.RightToLeft)]
    private static partial Regex VersionTokenRegex();
}
