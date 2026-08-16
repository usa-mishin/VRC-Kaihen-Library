# Repository instructions

作業開始時は `README.md` と `docs/DEVELOPMENT.md` を読み、`git status --short` と直近のコミットを確認すること。

- BLM の `%APPDATA%\pm.booth.library-manager\data.db` は読み取り専用。書き込み、置換、削除をしない。
- ユーザーデータ、商品ファイル、Unity プロジェクト、既存キャッシュを診断やテストの後処理で削除しない。
- `VrcKaihenManager` という旧名が残る箇所には DB・Unity bridge・キャッシュの移行処理があるため、用途を確認せず一括置換しない。
- 通常の検証ビルドは `dotnet build VrcKaihenLibrary.slnx -c Debug -p:Platform=x64`。
- 仕様、データ形式、互換処理、ビルドまたは配布手順を変更したら `docs/DEVELOPMENT.md` も更新する。
- 詳細な設計判断、BLM データの既知制約、Unity 導入規則は `docs/DEVELOPMENT.md` を正とする。
