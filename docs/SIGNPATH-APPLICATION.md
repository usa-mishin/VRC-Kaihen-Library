# SignPath Foundation application draft

Use this draft when applying for the free SignPath.io subscription. Do not include passwords, access tokens, certificate private keys, or local user data in the application.

## Project

- Project name: VRC改変ライブラリ（BLM拡張）
- Repository: https://github.com/usa-mishin/VRC-Kaihen-Library
- License: MIT
- Release: https://github.com/usa-mishin/VRC-Kaihen-Library/releases/tag/v1.0.72.0
- Signed artifact requested: Windows x64 self-contained EXE installer
- Build workflow: https://github.com/usa-mishin/VRC-Kaihen-Library/actions/workflows/build-installer.yml
- Code signing policy: https://github.com/usa-mishin/VRC-Kaihen-Library/blob/main/CODE_SIGNING_POLICY.md
- Privacy policy: https://github.com/usa-mishin/VRC-Kaihen-Library/blob/main/docs/PRIVACY.md

## Description

VRC改変ライブラリ（BLM拡張）は、ユーザーが自分のPCで使用しているBOOTH Library Managerの商品情報を、明示的な同意後に読み取り専用で整理し、VRChat向け改変アイテムをUnityへ導入するWindowsデスクトップアプリです。広告、テレメトリー、独自サーバーへのデータ送信はありません。ユーザーが選んだUnityインポートや重複整理以外に、商品ファイルやUnityプロジェクトを変更しません。

## Maintainer and roles

- Committer/reviewer: `usa-mishin` (repository owner)
- Approver: `usa-mishin` (release owner)

The maintainer verifies the tagged source, successful GitHub Actions run, artifact hash, release notes, and user-facing changes before approving each signing request. GitHub and SignPath multi-factor authentication must be enabled by the maintainer before submitting the application.

## Build and artifact verification

The tagged workflow checks out the source, installs .NET 8 and Inno Setup, runs `scripts/Build-ExeInstaller.ps1`, and uploads the installer plus SHA-256 file. The current tagged build succeeded in GitHub Actions run `33187054872`.

## Policy acknowledgements

- The project contains no malware or potentially unwanted behavior and is actively maintained.
- All project source code and build scripts are maintained by the repository owner.
- The project does not provide hacking or security-exploitation features.
- Installation, uninstallation, privacy, local data access, and system changes are documented in the repository.
- The initial Release is unsigned; signing will be requested only after SignPath approval.
