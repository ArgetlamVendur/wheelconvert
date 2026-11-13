# Review: PR #15 "chore: pin .NET 8 SDK with global.json" (2026-11-13)

**Verdict: APPROVE** (no required findings; 1 PR-description correction and 3 optional notes)

- Commit reviewed: `f53e484dab695ae0a6bb2b0f080d404d5a1f2a32` (branch `chore-pin-sdk`, base `main` @ `f879166`)
- Reviewer: Claude Fable 5.2, fresh session, did not see the build.
- Diff (`git diff origin/main...HEAD`): one new file, `global.json` (+7 lines). No workflow, product code,
  `Directory.Build.props`, contract, package or lock-file change. AGENTS §4.1 / §4.8 / §4.9 respected.

## CI status (latest commit f53e484): all green

| Run | Event | Job | Result | Runner image |
|---|---|---|---|---|
| 43390666054 | pull_request | core-linux | pass | ubuntu-24.04 **20261111.370.1** (the newer image that failed before) |
| 43390666054 | pull_request | windows | pass | windows-2025-vs2026 20261103.298.1 |
| 43390656359 | push | core-linux | pass | ubuntu-24.04 20261101.360.1 (older image) |
| 43390656359 | push | windows | pass | windows-2025-vs2026 20261103.298.1 |

core-linux on the newer image: 5 builds with "0 Warning(s) 0 Error(s)", Core tests 64/64, converter tests
54 / 113 (+8 skipped) / 64 passed. windows: build 0 warnings 0 errors, Core 64/64, App 87/87, converter tests
passed, publish step passed.

## What I ran locally (worktree, Windows, only SDK 8.0.428 installed)

| Check | Result |
|---|---|
| `dotnet --version` | `8.0.428` |
| `dotnet build WheelConvert.sln -c Release` (also with `--no-incremental`) | 0 warnings, 0 errors, exit 0 |
| `dotnet run --project tests/WheelConvert.Core.Tests -c Release` | 64/64 passed, exit code 0 |
| `dotnet test tests/WheelConvert.App.Tests -c Release` | 87 passed, 0 failed, exit 0 |
| conflict-marker `git grep` | prints nothing (exit 1 = no match) |
| `global.json` parses as JSON | yes |
| Reverted-fix check: version temporarily set to `99.0.100` | `dotnet --version` and `dotnet build` both exit 145 with "A compatible .NET SDK was not found. Requested SDK version: 99.0.100", naming the `global.json` path. Restored with `git checkout -- global.json`; `dotnet --version` is 8.0.428 again, tree clean. |

The reverted-fix check proves the file is read and that a non-matching pin fails loudly instead of silently
falling back. It cannot prove "a newer SDK is ignored" on this PC (only SDK 8 is installed); the CI run on image
20261111.370.1 is that proof.

## Does the pin do what is claimed?

Checked against https://learn.microsoft.com/en-us/dotnet/core/tools/global-json (page read today):

- `version` must be a full version; `8.0.400` is valid.
- `latestFeature`: "Uses the highest installed feature band and patch level that matches the requested major and
  minor with a feature band and patch level that's greater than or equal to the specified value. If not found,
  fails." The page's own example (8.0.302 + latestFeature) "allows 8.0.302 or any later 8.0.xxx version".
  So 8.0.428 and any later 8.0.4xx/5xx are accepted; 8.0.1xx to 8.0.3xx are rejected; a 9/10/11 SDK can never be
  selected (major and minor must match); with no qualifying SDK the command fails (confirmed locally, exit 145).
- Without `global.json`, "the highest installed SDK version is used (equivalent to setting `rollForward` to
  `latestMajor`)". This is the behaviour the PR removes.
- `allowPrerelease: false` is harmless here.
- The file is at the repo root, so both the CLI (searches up from the working directory) and the MSBuild SDK
  resolver (searches up from the solution/project directory) find it for every command in `build.yml`, in both
  jobs, and for local builds from any subfolder.

## Is the claimed cause supported? Proven vs inferred

Proven from logs and public data:
1. Same commit (`f5e1afe`, another PR), same workflow: core-linux fails on ubuntu-24.04 20261111.370.1
   (run 43388421145, `error CA2027` at `ConversionJobQueue.cs(202,37)`, 1 error) and passes on 20261101.360.1
   (run 43388411614). The difference is the runner image, not the code.
2. On the newer image setup-dotnet reports SDK 8.0.428 "already installed" (it adds nothing); on the older image
   it downloads 8.0.428.
3. The published image manifests (actions/runner-images, tags `ubuntu24/20261101.360` and `ubuntu24/20261111.370`)
   list the preinstalled SDKs: older image up to **10.0.403**; newer image up to 10.0.404 **plus 11.0.100**.
   The Windows image 20261103.298.1 lists up to 10.0.403 (no 11).
4. With `global.json` and nothing else changed, core-linux passes on the same newer image (run 43390666054).

Inferred (no build log prints the SDK version that compiled):
- That the failing build was compiled by a newer SDK. Given 1 to 4 this is the only reasonable reading, but it is
  not printed anywhere.
- Which SDK. The PR text says "apparently 10". The manifests point to **11.0.100** instead: the older image
  already had SDK 10.0.403 preinstalled and passed without a pin (so did the Windows job), and the only new major
  on the failing image is 11.0.100. This does not change the fix: `latestFeature` on 8.0 excludes 10 and 11 alike.

## Effect on the rest of the repo

- `windows` job: the same pin applies and it passed. That image does not ship SDK 11 yet; when it does, the pin
  prevents the same failure there. The publish step (`dotnet publish ... -r win-x64`) ran green under the pin.
- `actions/setup-dotnet` with `dotnet-version: 8.0.x` keeps installing the newest 8.0 SDK (currently 8.0.428),
  which satisfies ">= 8.0.400". No workflow change needed. (That the action ignores `global.json` when
  `dotnet-version` is given is from my memory of the action, not re-checked; the green runs are consistent with it.)
- Lock files: 7 `packages.lock.json` files exist and the converter projects set `RestorePackagesWithLockFile=true`.
  There is no `RestoreLockedMode` anywhere, so restore cannot fail on a lock mismatch. The pin makes the SDK choice
  more stable than before, not less.
- All projects target `net8.0` / `net8.0-windows`; nothing needs an SDK newer than 8.

## Findings

Required: none.

Optional / informational:
1. PR description, "Cause" paragraph: "uses the newest installed SDK (apparently 10)" is most likely wrong; the
   evidence points to SDK 11.0.100 (see above). Fix: correct the sentence before merge so the history is accurate.
   Not blocking.
2. `global.json:3-4`, downside: a machine with no 8.0 SDK at 8.0.400 or later (only 8.0.1xx to 3xx, or only
   9/10/11) can no longer build at all; every `dotnet` command inside the repo folder stops with "A compatible
   .NET SDK was not found" (exit 145). That is the intended, clear failure; the remedy is "install the .NET 8 SDK".
   Follow-up idea: one line in the README build instructions. Out of scope for this PR.
3. `global.json:3-4`: .NET 8 reaches end of support in November 2026, so runner images will eventually stop
   preinstalling it (setup-dotnet will still download it) and no new 8.0 SDKs will appear. The pin is a stopgap.
   The durable fix is the follow-up already named in the PR: fix CA2027 at
   `src/WheelConvert.Core/Jobs/ConversionJobQueue.cs:202`, and decide whether `AnalysisLevel=latest-recommended`
   should stay floating (a frozen-file change: needs user approval + ADR).
4. `global.json:4`: Microsoft's page recommends `rollForward: disable` when package lock files are used. Not needed
   here (no locked mode, and an exact pin would break CI every time `8.0.x` moves), so `latestFeature` is the
   better trade-off. No action.

## Checklist (docs/checklists/review.md)

- Safety (S1-S6, DragDropEffects, decoding, hooks, ProcessRunner, untrusted text): n/a, no code changed.
- Contract: only `global.json` added; no frozen file touched. This is a chore PR, not a WP, so there is no Owns
  list; the file is new repo-level build configuration. OK.
- Windows / interop: n/a.
- Quality: CI green (both jobs, both runs); Release build 0 warnings; App tests pass; Core tests pass;
  reverted-fix check done (see table); reviewer in a fresh session; no new packages; no dead code or TODOs;
  no user-facing text.
