# Code signing policy

Free code signing provided by [SignPath.io](https://signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

## Project and artifact

This policy applies to the source code in [usa-mishin/VRC-Kaihen-Library](https://github.com/usa-mishin/VRC-Kaihen-Library) and to release artifacts built from its tagged commits. The initial signed artifact is the self-contained x64 EXE installer produced by `scripts/Build-ExeInstaller.ps1`.

All signed artifacts are built by the repository's GitHub Actions workflow. Each signing request is tied to a tagged source commit and requires manual approval. The installer and application metadata use the project name `VRC改変ライブラリ（BLM拡張）` and the same four-part version.

## Team roles

- Committer and reviewer: `usa-mishin` (repository owner)
- Approver: `usa-mishin` (release owner)

Pull requests from anyone other than the committer require review before merging. The approver verifies the tag, build result, source diff, SHA-256 hash, and release notes before approving a signing request. GitHub and SignPath accounts use multi-factor authentication where supported.

## Privacy and security

The application does not transfer information to other networked systems unless specifically requested by the user or the person installing or operating it. It reads the local BOOTH Library Manager product information only after explicit consent and never writes to that source database. See [PRIVACY.md](docs/PRIVACY.md), [SECURITY.md](SECURITY.md), and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

The application is an unofficial tool and is not provided, endorsed, or partnered with by BOOTH or BOOTH Library Manager. Unity project changes and duplicate cleanup occur only after the user selects those operations.
