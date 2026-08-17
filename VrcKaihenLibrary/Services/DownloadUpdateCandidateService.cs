using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VrcKaihenLibrary.Services;

public static partial class DownloadUpdateCandidateService
{
    public static IReadOnlySet<string> FindUnityPackageCandidates(
        string rootPath,
        IEnumerable<string> unityPackagePaths,
        IEnumerable<string> latestDownloadableFileNames)
    {
        var packages = unityPackagePaths.ToList();
        var latest = latestDownloadableFileNames
            .Select(Path.GetFileNameWithoutExtension)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => CreateVersionedName(x!))
            .ToList();
        if (packages.Count == 0 || latest.Count == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var packagePath in packages)
        {
            var relativePath = Path.GetRelativePath(rootPath, packagePath);
            var components = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => CreateVersionedName(x!))
                .ToList();

            var matched = latest.Where(newFile => components.Any(local => IsSameFamily(local, newFile))).ToList();
            if (matched.Count == 0) continue;

            var hasExactLatestName = matched.Any(newFile => components.Any(local =>
                Normalize(local.Name).Equals(Normalize(newFile.Name), StringComparison.OrdinalIgnoreCase)));
            if (hasExactLatestName) continue;

            var hasOlderVersion = matched.Any(newFile => newFile.Version is not null && components.Any(local =>
                IsSameFamily(local, newFile)
                && local.Version is not null
                && local.Version < newFile.Version));
            var versionCouldNotBeCompared = matched.Any(newFile => components.Any(local =>
                IsSameFamily(local, newFile)
                && (local.Version is null || newFile.Version is null)));
            if (hasOlderVersion || versionCouldNotBeCompared) candidates.Add(packagePath);
        }

        // A single UnityPackage can be identified safely even when the downloadable ZIP and
        // its extracted contents use unrelated names. Avoid this fallback for multi-package products.
        if (candidates.Count == 0 && packages.Count == 1
            && !latest.Any(newFile => Normalize(relativeName(packages[0])).Contains(Normalize(newFile.Name), StringComparison.OrdinalIgnoreCase)))
            candidates.Add(packages[0]);

        return candidates;

        string relativeName(string path) => Path.GetRelativePath(rootPath, path);
    }

    private static string GetFamily(string value) => Normalize(VersionSuffixRegex().Replace(value, string.Empty));

    private static VersionedName CreateVersionedName(string value)
    {
        var withoutVersion = VersionSuffixRegex().Replace(value.Normalize(NormalizationForm.FormKC), string.Empty);
        var tokens = Regex.Split(withoutVersion, @"[^\p{L}\p{N}]+", RegexOptions.CultureInvariant)
            .Where(x => x.Length > 0)
            .Select(x => x.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new VersionedName(value, GetFamily(value), TryReadVersion(value), tokens);
    }

    private static bool IsSameFamily(VersionedName local, VersionedName latest)
    {
        if (local.Family.Length >= 4 && local.Family.Equals(latest.Family, StringComparison.OrdinalIgnoreCase)) return true;
        return local.Tokens.Count >= 2 && local.Tokens.All(latest.Tokens.Contains);
    }

    private static Version? TryReadVersion(string value)
    {
        var match = VersionTokenRegex().Match(value.Normalize(NormalizationForm.FormKC));
        if (!match.Success) return null;
        var parts = match.Groups[1].Value.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(x => !int.TryParse(x, out _))) return null;
        var numbers = parts.Select(int.Parse).Concat(Enumerable.Repeat(0, 4)).Take(4).ToArray();
        return new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
    }

    private static string Normalize(string value) => Regex.Replace(
        value.Normalize(NormalizationForm.FormKC), @"[^\p{L}\p{N}]", string.Empty,
        RegexOptions.CultureInvariant).ToLowerInvariant();

    private sealed record VersionedName(string Name, string Family, Version? Version, IReadOnlySet<string> Tokens);

    [GeneratedRegex(@"(?:^|[\s_.-])(?:v(?:er(?:sion)?)?\.?\s*)?\d+(?:[._-]\d+){1,3}[a-z]?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionSuffixRegex();

    [GeneratedRegex(@"(?:^|[\s_.-])(?:v(?:er(?:sion)?)?\.?\s*)?(\d+(?:[._-]\d+){1,3})(?:[a-z])?(?:$|[\s_.-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.RightToLeft)]
    private static partial Regex VersionTokenRegex();
}
