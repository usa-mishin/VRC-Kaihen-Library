using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace VrcKaihenLibrary.Services;

public sealed record DuplicateDownload(string KeepPath, string DeletePath, string TargetPath);

public sealed partial class DuplicateDownloadService
{
    [GeneratedRegex(@"\s*[（(](\d+)[）)]$")]
    private static partial Regex SuffixPattern();

    public IReadOnlyList<DuplicateDownload> FindDuplicateDownloads(string itemFolder)
    {
        if (!Directory.Exists(itemFolder)) return [];
        var directories = Directory.GetDirectories(itemFolder);
        var result = new List<DuplicateDownload>();

        foreach (var group in directories.GroupBy(path => SuffixPattern().Replace(Path.GetFileName(path), string.Empty), StringComparer.OrdinalIgnoreCase))
        {
            var members = group.Select(path => new
            {
                Path = path,
                Suffix = ParseSuffix(Path.GetFileName(path)),
                Modified = GetLatestWriteUtc(path)
            }).OrderByDescending(x => x.Modified).ThenByDescending(x => x.Suffix).ToList();
            if (members.Count < 2) continue;

            var keep = members[0];
            var targetPath = Path.Combine(itemFolder, group.Key);
            foreach (var candidate in members.Skip(1))
                result.Add(new DuplicateDownload(keep.Path, candidate.Path, targetPath));
        }
        return result;
    }

    public void KeepLatestAndNormalizeName(IEnumerable<DuplicateDownload> duplicates)
    {
        foreach (var group in duplicates.GroupBy(x => new { x.KeepPath, x.TargetPath }))
        {
            foreach (var duplicate in group)
                if (Directory.Exists(duplicate.DeletePath))
                    FileSystem.DeleteDirectory(duplicate.DeletePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            if (!group.Key.KeepPath.Equals(group.Key.TargetPath, StringComparison.OrdinalIgnoreCase)
                && Directory.Exists(group.Key.KeepPath) && !Directory.Exists(group.Key.TargetPath))
                Directory.Move(group.Key.KeepPath, group.Key.TargetPath);
        }
    }

    private static int ParseSuffix(string name)
    {
        var match = SuffixPattern().Match(name);
        return match.Success && int.TryParse(match.Groups[1].Value, out var value) ? value : 0;
    }

    private static DateTime GetLatestWriteUtc(string root)
    {
        var latest = Directory.GetLastWriteTimeUtc(root);
        foreach (var file in Directory.EnumerateFiles(root, "*", System.IO.SearchOption.AllDirectories))
            if (File.GetLastWriteTimeUtc(file) > latest) latest = File.GetLastWriteTimeUtc(file);
        return latest;
    }
}
