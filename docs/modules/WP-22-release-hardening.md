# WP-22: Release hardening and version from the tag

**Builder:** Sonnet · **Reviewer:** Opus (fresh session) · **Depends on:** WP-12 (done) · **Risk:** low · **Must merge before the next tag**

## Goal
Small fixes left over from WP-12, so the next release is built the same way every time, can't be created by accident, and reports its real version.

## Owns
- `.github/workflows/release.yml`, `.github/workflows/build.yml`
- `src/WheelConvert.Core/Jobs/ConversionJobQueue.cs`: the CA2027 fix only
- The place the app shows its version (find it: `git grep -n "Version" src/WheelConvert.App`), only if it reads the wrong attribute (req. 3)
- `docs/modules/WP-12-packaging-release.md`: notes only

## Must not touch
`Directory.Build.props` (its `<Version>0.1.0</Version>` stays as the default for local builds; CI overrides it), `installer/WheelConvert.iss` unless req. 3 proves it needs a change, any converter.

## Requirements
1. **Second guard against a release from a PR:** put the `draft-release` job in a GitHub **environment** named `release` (`environment: release`). The user creates it in Settings → Environments, with themselves as required reviewer and "Deployment branches and tags" limited to tags `v*`. Write those click steps in the PR description. The existing `if:` tag check stays.
2. **Pin Inno Setup:** `choco install innosetup --version <x.y.z> -y --no-progress`, with the version the last green run installed (read it from the `v0.1.0-rc1` run's log). Comment: "update deliberately, then re-run the installer hand tests".
3. **Version from the tag:** pass `-p:Version=$env:VERSION` to `dotnet build` and `dotnet publish` in `release.yml` (and `0.0.0-pr<N>` for PR runs, as today). Result: the exe's file version is `VERSION_NUMERIC` (e.g. `0.1.0.0`) and its product version / `InformationalVersion` is the full tag (`0.1.0-rc2`). If the tray or About text shows a version, it must show the full one. `build.yml` keeps the default (local and CI test builds say `0.1.0`).
4. **CA2027 at `ConversionJobQueue.cs:202`:** fix the cause; don't suppress it. Today CI passes only because the SDK pin (global.json) uses an analyzer version that doesn't raise it; with `TreatWarningsAsErrors` a newer SDK would break the build.
5. **%TEMP% leftovers check:** the app is published with native DLLs beside the exe (`IncludeNativeLibrariesForSelfExtract=false`). Confirm whether a single-file extraction folder is still created in `%TEMP%\.net\WheelConvert\`: write what you find in the PR. If one is created, add its removal to the uninstaller; if not, close the follow-up.

## Acceptance tests
| # | Who | Test | Expected | Result |
|---|---|---|---|---|
| 1 | Agent | PR run of `release.yml` | Green; `draft-release` skipped; artifacts as before | PENDING (filled after the PR run, see PR) |
| 2 | Agent | Log of the PR run | Inno Setup installed at the pinned version | PENDING (6.7.1 pinned; confirm in the PR run log) |
| 3 | Agent | PR artifact exe: read its version resource | File version `0.0.0.0`, product version `0.0.0-pr<N>` | PASS locally with `-p:Version=0.0.0-pr99`: FileVersion 0.0.0.0, ProductVersion 0.0.0-pr99. PR artifact check PENDING |
| 4 | Agent | `dotnet build -c Release` with the newest .NET 8 SDK (not the pinned one) | No CA2027 | PARTIAL: only SDK 8.0.428 is installed here, which never raised CA2027. Cause removed (Task.Delay race replaced by WaitAsync); not verified on a newer SDK |
| 5 | User | Create the `release` environment as the PR describes | Done | NEEDS USER |
| 6 | User | Next tag (`v0.1.0-rc2` or `v0.2.0-…`): the run waits for your approval before creating the draft | Approve → draft appears; the installed exe's Properties → Details show the tag's version | NEEDS USER |

## Out of scope
Code signing (still pending, see GO-NO-GO.md §5), winget submission.

## Start prompt
```
Read AGENTS.md, docs/STATUS.md, docs/modules/WP-12-packaging-release.md, docs/modules/WP-22-release-hardening.md, .github/workflows/release.yml.
You are the builder for WP-22. Create branch wp-22-release-hardening from main. Restate the plan in 3–5 lines and wait for "go".
```
