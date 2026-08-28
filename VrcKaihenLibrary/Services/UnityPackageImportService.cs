using System;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using VrcKaihenLibrary.Models;

namespace VrcKaihenLibrary.Services;

public sealed record ImportPreparationProgress(int Percentage, string Message);

public sealed class UnityPackageImportService
{
    private const string RepackFormatVersion = "safe-dotnet-extract-gzip-store-v4-ascii-cache-name";
    private const int MaximumArchiveEntries = 100_000;
    private const long MaximumExpandedBytes = 20L * 1024 * 1024 * 1024;
    private const long MinimumExpansionAllowance = 512L * 1024 * 1024;

    public string PrepareForImport(
        LibraryItem item,
        string sourcePackagePath,
        string? targetFolderName,
        Action<ImportPreparationProgress>? reportProgress = null)
    {
        var sourceInfo = new FileInfo(sourcePackagePath);
        if (!sourceInfo.Exists) throw new FileNotFoundException("Unityパッケージが見つかりません。", sourcePackagePath);
        reportProgress?.Invoke(new(2, "キャッシュを確認しています"));
        var libraryRoot = Path.GetDirectoryName(item.FolderPath)
            ?? throw new DirectoryNotFoundException("BLMライブラリの保存先を特定できません。");
        var sharedCacheRoot = Path.Combine(libraryRoot, ".VrcKaihenLibraryImportCache");
        var legacyCacheRoot = Path.Combine(libraryRoot, ".VrcKaihenManagerImportCache");
        if (!Directory.Exists(sharedCacheRoot) && Directory.Exists(legacyCacheRoot))
        {
            try { Directory.Move(legacyCacheRoot, sharedCacheRoot); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        var cacheRoot = Path.Combine(sharedCacheRoot, item.RegistrationId);
        Directory.CreateDirectory(cacheRoot);
        try { File.SetAttributes(sharedCacheRoot, File.GetAttributes(sharedCacheRoot) & ~FileAttributes.Hidden); }
        catch { }

        var cacheKey = $"{RepackFormatVersion}|{sourceInfo.FullName}|{sourceInfo.Length}|{sourceInfo.LastWriteTimeUtc.Ticks}|{targetFolderName}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)))[..16];
        // Unity 2022.3 passes the package filename through its bundled 7-Zip process. On some
        // Windows locales, non-ASCII filenames are corrupted while Unity creates its short path,
        // causing a valid archive to fail with "Couldn't decompress package". Keep the complete
        // path handed to Unity ASCII-only; the original download is never renamed or modified.
        var destinationPath = Path.Combine(cacheRoot, $"package-{hash}.unitypackage");
        if (File.Exists(destinationPath))
        {
            reportProgress?.Invoke(new(100, "準備済みキャッシュを使用します"));
            return destinationPath;
        }

        if (string.IsNullOrWhiteSpace(targetFolderName))
        {
            reportProgress?.Invoke(new(5, "Unityパッケージを安全に検査しています"));
            ValidateArchiveSafely(sourcePackagePath, sourceInfo.Length, progress =>
                reportProgress?.Invoke(new(5 + (int)(progress * 85), "Unityパッケージを安全に検査しています")));
            var directCopyTemporaryPath = destinationPath + $".copying-{Guid.NewGuid():N}";
            try
            {
                reportProgress?.Invoke(new(90, "Unity向けの安全なファイル名で準備しています"));
                CopyFileWithProgress(sourcePackagePath, directCopyTemporaryPath, percentage =>
                    reportProgress?.Invoke(new(90 + (int)(percentage * 10), "Unity向けの安全なファイル名で準備しています")));
                File.Move(directCopyTemporaryPath, destinationPath);
                reportProgress?.Invoke(new(100, "Unityへの受け渡し準備が完了しました"));
                return destinationPath;
            }
            finally
            {
                TryDeleteFile(directCopyTemporaryPath);
            }
        }

        var workingRoot = Path.Combine(Path.GetTempPath(), "VrcKaihenLibrary", "UnityPackagePreparation", Guid.NewGuid().ToString("N"));
        var stagingRoot = Path.Combine(workingRoot, "archive");
        var temporaryPath = Path.Combine(workingRoot, "prepared.unitypackage");
        var entryListPath = Path.Combine(workingRoot, "entries.txt");
        var cacheTemporaryPath = destinationPath + $".copying-{Guid.NewGuid():N}";
        Directory.CreateDirectory(stagingRoot);

        try
        {
            reportProgress?.Invoke(new(6, "Unityパッケージを安全に検査・展開しています"));
            ExtractArchiveSafely(sourcePackagePath, stagingRoot, sourceInfo.Length, progress =>
                reportProgress?.Invoke(new(6 + (int)(progress * 34), "Unityパッケージを安全に検査・展開しています")));
            reportProgress?.Invoke(new(40, "配置先を変更しています"));

            var pathnameFiles = Directory.EnumerateFiles(stagingRoot, "pathname", SearchOption.AllDirectories).ToArray();
            for (var index = 0; index < pathnameFiles.Length; index++)
            {
                var pathnameFile = pathnameFiles[index];
                var pathname = File.ReadAllText(pathnameFile, Encoding.UTF8).TrimEnd('\0', '\r', '\n');
                File.WriteAllText(pathnameFile, RewriteAssetPath(pathname, targetFolderName), new UTF8Encoding(false));
                var progress = pathnameFiles.Length == 0 ? 50 : 40 + (int)((index + 1d) / pathnameFiles.Length * 10);
                reportProgress?.Invoke(new(progress, "配置先を変更しています"));
            }

            var rootEntries = Directory.EnumerateFileSystemEntries(stagingRoot)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            if (rootEntries.Length == 0)
                throw new InvalidDataException("Unityパッケージの内容が空です。");

            File.WriteAllLines(entryListPath, rootEntries!, new UTF8Encoding(false));
            reportProgress?.Invoke(new(52, "Unity互換形式で高速圧縮しています"));
            RunTarWithProgress(temporaryPath, sourceInfo.Length, ratio =>
                reportProgress?.Invoke(new(52 + (int)(Math.Min(1, ratio) * 46), "Unity互換形式で高速圧縮しています")),
                "--format=ustar", "--options", "gzip:compression-level=0",
                "-czf", temporaryPath, "-C", stagingRoot, "-T", entryListPath);

            if (!File.Exists(temporaryPath) || new FileInfo(temporaryPath).Length == 0)
                throw new InvalidDataException("Unityパッケージの再生成に失敗しました。");
            reportProgress?.Invoke(new(98, "準備済みキャッシュを保存しています"));
            CopyFileWithProgress(temporaryPath, cacheTemporaryPath, percentage =>
                reportProgress?.Invoke(new(98 + (int)(percentage * 2), "準備済みキャッシュを保存しています")));
            File.Move(cacheTemporaryPath, destinationPath);
            reportProgress?.Invoke(new(100, "Unityへの受け渡し準備が完了しました"));
            return destinationPath;
        }
        finally
        {
            TryDeleteFile(cacheTemporaryPath);
            TryDeleteDirectory(workingRoot);
        }
    }

    private static void ExtractArchiveSafely(string packagePath, string stagingRoot, long compressedBytes, Action<double>? reportProgress)
    {
        var expansionLimit = GetExpansionLimit(compressedBytes);
        var root = Path.GetFullPath(stagingRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        long expandedBytes = 0;
        var entryCount = 0;
        using var packageStream = File.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var gzipStream = new GZipStream(packageStream, CompressionMode.Decompress, leaveOpen: false);
        using var reader = new TarReader(gzipStream, leaveOpen: false);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            entryCount++;
            if (entryCount > MaximumArchiveEntries)
                throw new InvalidDataException($"Unityパッケージの項目数が安全上の上限（{MaximumArchiveEntries:N0}件）を超えています。");
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.Directory))
                throw new InvalidDataException($"リンクまたは特殊項目を含むUnityパッケージは展開できません: {entry.Name}");

            var destinationPath = ResolveSafeArchivePath(root, entry.Name);
            if (entry.EntryType == TarEntryType.Directory)
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            if (entry.Length < 0 || expandedBytes > expansionLimit - entry.Length)
                throw new InvalidDataException($"Unityパッケージの展開容量が安全上の上限（{expansionLimit / 1024 / 1024:N0}MB）を超えています。");
            expandedBytes += entry.Length;
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            if (entry.DataStream is null)
                throw new InvalidDataException($"内容を読み取れないUnityパッケージ項目があります: {entry.Name}");
            using var destination = File.Open(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            entry.DataStream.CopyTo(destination);
            reportProgress?.Invoke(expansionLimit == 0 ? 1 : Math.Min(0.99, (double)expandedBytes / expansionLimit));
        }
        if (entryCount == 0) throw new InvalidDataException("Unityパッケージの内容が空です。");
        reportProgress?.Invoke(1);
    }

    private static void ValidateArchiveSafely(string packagePath, long compressedBytes, Action<double>? reportProgress)
    {
        var expansionLimit = GetExpansionLimit(compressedBytes);
        long expandedBytes = 0;
        var entryCount = 0;
        using var packageStream = File.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var gzipStream = new GZipStream(packageStream, CompressionMode.Decompress, leaveOpen: false);
        using var reader = new TarReader(gzipStream, leaveOpen: false);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            entryCount++;
            if (entryCount > MaximumArchiveEntries)
                throw new InvalidDataException($"Unityパッケージの項目数が安全上の上限（{MaximumArchiveEntries:N0}件）を超えています。");
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.Directory))
                throw new InvalidDataException($"リンクまたは特殊項目を含むUnityパッケージは使用できません: {entry.Name}");
            _ = ResolveSafeArchivePath(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, entry.Name);
            if (entry.EntryType == TarEntryType.Directory) continue;
            if (entry.Length < 0 || expandedBytes > expansionLimit - entry.Length)
                throw new InvalidDataException($"Unityパッケージの展開容量が安全上の上限（{expansionLimit / 1024 / 1024:N0}MB）を超えています。");
            if (entry.DataStream is null)
                throw new InvalidDataException($"Unityパッケージのファイルデータを読み取れません: {entry.Name}");
            // 検証だけの場合も末尾まで読み、途中で切れた gzip/tar を検出する。
            entry.DataStream.CopyTo(Stream.Null);
            expandedBytes += entry.Length;
            reportProgress?.Invoke(expansionLimit == 0 ? 1 : Math.Min(0.99, (double)expandedBytes / expansionLimit));
        }
        if (entryCount == 0) throw new InvalidDataException("Unityパッケージの内容が空です。");
        reportProgress?.Invoke(1);
    }

    private static long GetExpansionLimit(long compressedBytes) =>
        Math.Min(MaximumExpandedBytes, Math.Max(MinimumExpansionAllowance,
            compressedBytes > MaximumExpandedBytes / 200 ? MaximumExpandedBytes : compressedBytes * 200));

    private static string ResolveSafeArchivePath(string extractionRoot, string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) || entryName.IndexOf('\0') >= 0)
            throw new InvalidDataException("名前が空、または不正なUnityパッケージ項目があります。");
        var normalized = entryName.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidDataException($"絶対パスを含むUnityパッケージは展開できません: {entryName}");
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(IsUnsafePathSegment))
            throw new InvalidDataException($"安全でないパスを含むUnityパッケージは展開できません: {entryName}");
        var destinationPath = Path.GetFullPath(Path.Combine(extractionRoot, Path.Combine(segments)));
        if (!destinationPath.StartsWith(extractionRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"展開先の外部を指すUnityパッケージ項目があります: {entryName}");
        return destinationPath;
    }

    private static bool IsUnsafePathSegment(string segment)
    {
        if (segment is "." or ".." || segment.EndsWith(' ') || segment.EndsWith('.')
            || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return true;
        var baseName = segment.Split('.')[0];
        return baseName.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || baseName.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || baseName.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || baseName.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (baseName.Length == 4 && (baseName.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                || baseName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && baseName[3] is >= '1' and <= '9');
    }

    private static void RunTarWithProgress(
        string outputPath,
        long expectedSize,
        Action<double>? reportProgress,
        params string[] arguments)
    {
        var tarPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");
        if (!File.Exists(tarPath))
            throw new FileNotFoundException("Windows標準のtar.exeが見つかりません。", tarPath);

        var startInfo = new ProcessStartInfo(tarPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unityパッケージの再生成処理を開始できませんでした。");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        while (!process.WaitForExit(100))
        {
            var currentSize = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
            reportProgress?.Invoke(expectedSize <= 0 ? 0 : Math.Min(0.98, (double)currentSize / expectedSize));
        }
        var standardOutput = standardOutputTask.GetAwaiter().GetResult();
        var standardError = standardErrorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            throw new InvalidDataException($"Unityパッケージの再生成に失敗しました。\n{standardError}\n{standardOutput}".Trim());
    }

    private static void CopyFileWithProgress(string sourcePath, string destinationPath, Action<double>? reportProgress)
    {
        using var source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var destination = File.Open(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[1024 * 1024];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            destination.Write(buffer, 0, read);
            reportProgress?.Invoke(source.Length == 0 ? 1 : (double)source.Position / source.Length);
        }
    }

    private static string RewriteAssetPath(string pathname, string category)
    {
        var normalized = pathname.Replace('\\', '/');
        if (normalized.Equals("Assets", StringComparison.OrdinalIgnoreCase)) return $"Assets/{category}";
        if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return pathname;
        var relativePath = normalized["Assets/".Length..];
        if (relativePath.Equals(category, StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith(category + "/", StringComparison.OrdinalIgnoreCase)) return normalized;
        return $"Assets/{category}/{relativePath}";
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }
}
