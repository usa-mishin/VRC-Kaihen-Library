using System.Collections.Generic;

namespace VrcKaihenLibrary.Models;

public static class AssetCategories
{
    public const string Avatar = "アバター";
    public const string Other = "その他";
    public const string Unclassified = Other;
    public static IReadOnlyList<string> All { get; } =
    [
        Avatar, "衣装", "髪型", "アクセサリー", "テクスチャ", "マテリアル",
        "ギミック", "アニメーション", "ツール", "シェーダー", "ワールド", Other
    ];
}
