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

## Windowsへのインストール（MSIX）

一般配布では、コード署名済みの`.msix`をWindows標準のApp Installerで開いてインストールします。アンインストールはWindowsの「設定 > アプリ > インストールされているアプリ」から行います。

開発環境でテスト用MSIXを作成する場合は、PowerShellで次を実行します。

```powershell
$thumbprint = .\scripts\New-DevelopmentCertificate.ps1
.\scripts\Build-Msix.ps1 -Version 1.0.0.0 -Platform x64 -CertificateThumbprint $thumbprint
```

自己署名した開発版をインストールするPCでは、管理者として起動したPowerShellから証明書を一度だけ信頼します。

```powershell
.\scripts\Trust-DevelopmentCertificate.ps1 -CertificateThumbprint <上で表示されたThumbprint>
```

その後、`artifacts\msix`配下の`.msix`をダブルクリックするか、次を実行します。

```powershell
.\scripts\Install-Msix.ps1 -PackagePath <生成されたMSIXのパス>
```

`New-AppInstaller.ps1`は、配布URL確定後に自動更新用の`.appinstaller`を生成するためのスクリプトです。正式配布では自己署名証明書を使用せず、信頼されたコード署名サービスまたは証明書で署名してください。

対象は .NET 8 / WinUI 3 / Windows 10 19041以降です。

## 開発ドキュメント

設計、分類規則、実装状況、今後の作業は [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) を参照してください。仕様や設計を変更した場合は、コードと同じ変更でこのREADMEまたは開発ドキュメントも更新してください。
