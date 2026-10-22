# How to use this base (for you, the human)

The agents read `AGENTS.md`. This page is for you.

## What you have
- **Core** (`src/WheelConvert.Core`): the rules and plumbing every module shares — finished, compiled and tested (all tests pass; independent review in `docs/reviews/`).
- **Stubs** for every other module: they compile (converters) or should compile (the Windows app), do nothing yet, and say exactly which work package (WP) fills them in.
- **12 work packages** in `docs/modules/`: each one is a self-contained brief with the files it owns, requirements, gotchas, tests and a copy-paste start prompt.

## The loop for each module
1. Open `docs/STATUS.md`, pick the next `TODO` WP (suggested order is in `docs/modules/README.md`).
2. Open the builder tool named in the WP (Claude Code or Antigravity) in the repo folder and paste the WP's **Start prompt**.
3. Agent restates the plan → you say "go" → it builds, runs its checks, opens a PR.
4. Run the "User" tests in the WP table yourself (scratch files only, `D:\Claude Base\Cowork Playground\APPS\wc-test\`).
5. Open the **other** tool and paste:
   ```
   Read AGENTS.md and docs/checklists/review.md. Review the PR for WP-NN against docs/modules/WP-NN-*.md.
   Write docs/reviews/WP-NN-<date>.md with APPROVE or CHANGES NEEDED and numbered findings. Don't rewrite the code.
   ```
6. Approved + your tests pass → merge on GitHub → `git checkout main && git pull`.

## Start here
1. Push this repo to GitHub (Part A of `docs/archive/phase0/2026-10-20-multi-agent-handoff-guide.md` still applies — use your `APPS\wheelconvert` folder).
2. Watch the **Actions** tab: the first build is the first time the Windows app is compiled. If it fails, give the log to Claude Code with: "Fix the CI build. Only touch files under src/WheelConvert.App. Follow AGENTS.md."
3. Then WP-11 → WP-05 → WP-03 → WP-04 → WP-06 (first real conversion).

## What can go wrong
- **The app shell has never been compiled.** Expect a small fix-up round on the first CI run.
- **The Shift-drag gesture is still unproven.** The hotkey works regardless; WP-12 runs the antivirus check that decides the default.
- **Two tools on the same branch overwrite each other.** One tool per branch, always.
- **An agent "improves" a contract.** Reject any PR that touches `src/WheelConvert.Core/Contracts/`, `TargetCatalog.cs` or `docs/CONTRACTS.md` without your explicit approval.
