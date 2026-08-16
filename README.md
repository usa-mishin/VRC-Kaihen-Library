# 改変ライブラリ（VrcKaihenManager）

アバター編集ではメイン呼称、複数の識別タグ、登録済みアバターから複数選べる共通素体関係を設定できます。識別タグは大文字小文字と全角半角を無視して対応判定に使われ、詳細画面のタグをクリックするとBOOTH検索を開きます。

BOOTH Library Manager（BLM）がダウンロードしたVRChat向け商品を整理し、Unityプロジェクトへの導入を支援するWindowsデスクトップアプリです。

## 現在の機能

- BLMのSQLiteデータベースと商品保存先を読み取り専用で参照
- サムネイル付き商品一覧、検索、分類タブ
- BOOTHカテゴリに基づく自動分類
- 詳細画面で分類とAssets直下配置フラグを編集
- 商品保存フォルダーとBOOTH商品ページを開く
- ユーザー設定をアプリ専用SQLite DBへ保存

## データと安全性

- BLM DB: `%APPDATA%\pm.booth.library-manager\data.db`
- アプリ独自DB: `%LOCALAPPDATA%\VrcKaihenManager\library.db`
- BLM DBには書き込みません。分類などの編集結果は必ずアプリ独自DBへ保存します。

## ビルド

```powershell
dotnet build VrcKaihenManager.slnx -c Debug -p:Platform=x64
```

対象は .NET 8 / WinUI 3 / Windows 10 19041以降です。

## 開発ドキュメント

設計、分類規則、実装状況、今後の作業は [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) を参照してください。仕様や設計を変更した場合は、コードと同じ変更でこのREADMEまたは開発ドキュメントも更新してください。
