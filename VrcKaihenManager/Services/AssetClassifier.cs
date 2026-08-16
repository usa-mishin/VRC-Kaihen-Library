using System;
using VrcKaihenManager.Models;

namespace VrcKaihenManager.Services;

public static class AssetClassifier
{
    // BOOTHの親カテゴリ・子カテゴリを最優先かつ唯一の自動分類基準とする。
    // DBの世代差により親カテゴリが取れない場合もあるため、子カテゴリ名でも一致させる。
    private static readonly (string BoothSubcategory, string AppCategory)[] BoothCategoryRules =
    [
        ("3Dキャラクター", AssetCategories.Avatar),
        ("3D衣装", "衣装"),
        ("3D髪型", "髪型"),
        ("3D装飾品", "アクセサリー"),
        ("3D靴", "衣装"),
        ("3D小道具", "ギミック"),
        ("3Dテクスチャ", "テクスチャ"),
        ("3D素材・マテリアル", "マテリアル"),
        ("3Dマテリアル", "マテリアル"),
        ("3Dツール・システム", "ツール"),
        ("3Dモーション・アニメーション", "アニメーション"),
        ("3D環境・ワールド", "ワールド")
    ];

    public static string Classify(LibraryItem item)
    {
        foreach (var (boothSubcategory, appCategory) in BoothCategoryRules)
            if (item.OriginalCategory.Contains(boothSubcategory, StringComparison.OrdinalIgnoreCase))
                return appCategory;

        return AssetCategories.Other;
    }
}
