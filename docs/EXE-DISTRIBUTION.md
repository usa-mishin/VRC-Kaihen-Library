# EXE インストーラーの配布と更新

自己完結型の非パッケージ版 WinUI 3 アプリを Inno Setup 6 で単一のセットアップ EXE にします。MSIX と異なり、配布先PCで自己署名証明書を登録する必要はありません。ただし、コード署名のないEXEは Microsoft Defender SmartScreen の警告対象になる場合があります。

## 開発PCの準備

```powershell
winget install --id JRSoftware.InnoSetup -e
```

.NET 8 SDK と Visual Studio の WinUI 3 開発環境も従来どおり必要です。

## 生成

前回より大きい4区切りの版番号を指定します。

```powershell
.\scripts\Build-ExeInstaller.ps1 -Version 1.0.6.0 -Platform x64
```

生成物:

```text
artifacts/installer/VrcKaihenLibrary-1.0.6.0-x64-setup.exe
```

インストール先はユーザー単位の `%LOCALAPPDATA%\Programs\VrcKaihenLibrary` です。管理者権限は要求しません。通常の更新では同じ `AppId` の既存版を検出して上書きします。設定DB `%LOCALAPPDATA%\VrcKaihenLibrary\library.db` はインストール先の外にあるため、更新や通常のアンインストールでは削除しません。

## MSIX版からの移行

MSIX版とEXE版は別のインストールとして扱われます。二重起動を避けるため、最初にWindowsの「インストールされているアプリ」からMSIX版をアンインストールし、その後EXE版をインストールしてください。設定DBは共通の保存場所に残るため引き継がれます。

### アプリ一覧に2つ表示される場合

`usa-mishin.VrcKaihenLibrary` が残っている場合は、開発用または旧版のMSIXです。MSIXとEXEはWindows上で別アプリとして登録されるため、アプリ一覧に2件表示されます。MSIX側の古いショートカットをタスクバーから起動すると、旧バージョン番号や旧アイコンが表示されます。EXE版を使用する場合は、MSIXをアンインストールしてから、`%LOCALAPPDATA%\Programs\VrcKaihenLibrary\VrcKaihenLibrary.exe` のショートカットをタスクバーへ登録し直してください。EXE版のスタートメニュー・デスクトップショートカットは`Assets\AppIcon.ico`を明示的に参照します。既存のタスクバー固定はアイコンキャッシュが残ることがあるため、固定解除後に再固定します。

## GitとGitHub Releasesの準備

Git自体に特別な拡張は不要です。配布するコミットを確定し、版番号と同じタグを付けてGitHubへ送ります。

```powershell
git status --short
git push origin main
git tag -a v1.0.6 -m "VrcKaihenLibrary 1.0.6"
git push origin v1.0.6
```

GitHubのリポジトリで「Releases」→「Draft a new release」を開き、`v1.0.6` タグを選択して、生成したセットアップEXEとSHA-256ハッシュを添付します。`artifacts/` はGit管理対象外のままにし、大きなバイナリを通常のコミットへ含めないでください。

公開前に、別のWindowsユーザーまたはテストPCで次を確認します。

1. 新規インストールと起動
2. 旧版を入れた状態からの上書き更新
3. 分類・アバター設定が更新後も残ること
4. アンインストール後にアプリ本体とショートカットが消えること
5. `%LOCALAPPDATA%\VrcKaihenLibrary` のユーザーデータが残ること

## 将来コード署名する場合

信頼されたコード署名証明書を取得した後は、セットアップEXEに署名できます。配布先での証明書登録は不要になり、発行元表示とSmartScreen評価の面で有利になります。秘密鍵やPFXはGitへコミットしないでください。

## SignPath Foundation申請用の再現ビルド

公開リポジトリには `.github/workflows/build-installer.yml` を配置し、`v1.0.72.0` のような4区切りタグ、またはActionsの手動実行からWindowsランナーでセットアップEXEとSHA-256ファイルを生成します。署名サービスへ申請する際は [CODE_SIGNING_POLICY.md](../CODE_SIGNING_POLICY.md) と、GitHub Actionsの成功履歴・既存Releaseを提示します。署名リクエストはリリース担当者が手動承認し、署名秘密鍵はリポジトリへ保存しません。
