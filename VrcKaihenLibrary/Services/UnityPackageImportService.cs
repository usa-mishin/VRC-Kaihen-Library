using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using VrcKaihenLibrary.Models;

namespace VrcKaihenLibrary.Services;

public sealed record ImportPreparationProgress(int Percentage, string Message);

public sealed class UnityPackageImportService
{
    private const string RepackFormatVersion = "bsdtar-gzip-store-fast-work-v2";

    public string PrepareForImport(
        LibraryItem item,
        string sourcePackagePath,
        string? targetFolderName,
        Action<ImportPreparationProgress>? reportProgress = null)
    {
        if (string.IsNullOrWhiteSpace(targetFolderName))
        {
            reportProgress?.Invoke(new(100, "元のUnityパッケージを使用します"));
            return sourcePackagePath;
        }

        reportProgress?.Invoke(new(2, "キャッシュを確認しています"));
        var sourceInfo = new FileInfo(sourcePackagePath);
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
        try { File.SetAttributes(sharedCacheRoot, File.GetAttributes(sharedCacheRoot) | FileAttributes.Hidden); }
        catch { }

        var cacheKey = $"{RepackFormatVersion}|{sourceInfo.FullName}|{sourceInfo.Length}|{sourceInfo.LastWriteTimeUtc.Ticks}|{targetFolderName}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey)))[..16];
        var safeName = string.Concat(Path.GetFileNameWithoutExtension(sourceInfo.Name)
            .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var destinationPath = Path.Combine(cacheRoot, $"{safeName}.{hash}.unitypackage");
        if (File.Exists(destinationPath))
        {
            reportProgress?.Invoke(new(100, "準備済みキャッシュを使用します"));
            return destinationPath;
        }

        var workingRoot = Path.Combine(Path.GetTempPath(), "VrcKaihenLibrary", "UnityPackagePreparation", Guid.NewGuid().ToString("N"));
        var stagingRoot = Path.Combine(workingRoot, "archive");
        var temporaryPath = Path.Combine(workingRoot, "prepared.unitypackage");
        var entryListPath = Path.Combine(workingRoot, "entries.txt");
        var cacheTemporaryPath = destinationPath + $".copying-{Guid.NewGuid():N}";
        Directory.CreateDirectory(stagingRoot);

        try
        {
            reportProgress?.Invoke(new(6, "Unityパッケージを検査しています"));
            ValidateArchiveEntriesWithTar(sourcePackagePath);
            reportProgress?.Invoke(new(15, "Unityパッケージを展開しています"));
            RunTarFromArchive(sourcePackagePath, "-xzf", "-", "-C", stagingRoot);
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

    private static void ValidateArchiveEntriesWithTar(string packagePath)
    {
        var output = RunTarFromArchive(packagePath, "-tzf", "-");
        foreach (var entryName in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = entryName.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal)
                || normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
                throw new InvalidDataException("安全でないパスを含むUnityパッケージは展開できません。");
        }
    }

    private static string RunTar(params string[] arguments)
    {
        var tarPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");
        if (!File.Exists(tarPath)) throw new FileNotFoundException("Windows標準のtar.exeが見つかりません。", tarPath);
        var startInfo = new ProcessStartInfo(tarPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unityパッケージ処理を開始できませんでした。");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidDataException($"Unityパッケージ処理に失敗しました。\n{standardError}\n{standardOutput}".Trim());
        return standardOutput;
    }

    private static string RunTarFromArchive(string packagePath, params string[] arguments)
    {
        var tarPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe");
        if (!File.Exists(tarPath)) throw new FileNotFoundException("Windows標準のtar.exeが見つかりません。", tarPath);
        var startInfo = new ProcessStartInfo(tarPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unityパッケージ処理を開始できませんでした。");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        try
        {
            using var source = File.Open(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            source.CopyTo(process.StandardInput.BaseStream);
        }
        finally
        {
            process.StandardInput.Close();
        }
        process.WaitForExit();
        var standardOutput = standardOutputTask.GetAwaiter().GetResult();
        var standardError = standardErrorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            throw new InvalidDataException($"Unityパッケージ処理に失敗しました。\n{standardError}\n{standardOutput}".Trim());
        return standardOutput;
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
