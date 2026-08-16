using System;
using System.IO;
using System.Linq;
using VrcKaihenLibrary.Models;

namespace VrcKaihenLibrary.Services;

public static class UnityImportTargetResolver
{
    public static string Resolve(string unityProjectPath, LibraryItem item)
    {
        if (string.IsNullOrWhiteSpace(unityProjectPath))
            throw new ArgumentException("Unityプロジェクトを指定してください。", nameof(unityProjectPath));

        var assetsPath = Path.Combine(Path.GetFullPath(unityProjectPath), "Assets");
        return item.Category is AssetCategories.Avatar or "ワールド" || item.ImportToAssetsRoot
            ? assetsPath
            : Path.Combine(assetsPath, AssetCategories.All.Contains(item.Category) ? item.Category : AssetCategories.Unclassified);
    }
}
