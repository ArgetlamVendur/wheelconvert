# How to run WheelConvert with Claude Code + Antigravity

This guide is for you (the human). The agents read `AGENTS.md`; you read this.

---

## Part A — Put the repo on GitHub (one time, ~15 min)

### A1. Install the tools (Windows)
Open **PowerShell** and run:
```powershell
winget install --id Git.Git -e
winget install --id GitHub.cli -e
winget install --id Microsoft.DotNet.SDK.8 -e
```
Close and reopen PowerShell after installing.

### A2. Tell Git who you are
```powershell
git config --global user.name "Your Name"
git config --global user.email "you@example.com"
git config --global init.defaultBranch main
```
Use the email on your GitHub account. To keep your real email private, use the `@users.noreply.github.com` address shown under GitHub → Settings → Emails.

### A3. Log in to GitHub from the terminal
```powershell
gh auth login
```
Choose: **GitHub.com → HTTPS → Yes (authenticate Git) → Login with a web browser**. Paste the code it shows into the browser.

### A4. Unzip and create the repo
1. The repo sits at `D:\Claude Base\Cowork Playground\APPS\wheelconvert`. Make sure that folder is not synced by OneDrive or Google Drive — syncing fights with Git. The path contains spaces, so always wrap it in quotes in PowerShell.
2. In PowerShell:
```powershell
cd "D:\Claude Base\Cowork Playground\APPS\wheelconvert"
git init
git add .
git commit -m "chore: initial scaffold with agent rules and Phase 0 tasks"
gh repo create wheelconvert --private --source=. --remote=origin --push
```
Start **private**. Flip to public at release time (repo → Settings → Change visibility).

### A5. Protect `main` (stops agents committing to it directly)
On github.com → your repo → **Settings → Branches → Add branch ruleset** (or "Add rule"):
- Branch name pattern: `main`
- Tick **Require a pull request before merging**
- Tick **Block force pushes**
Save. Note: on private repos with a free account, rulesets may not be enforced — then the rule in `AGENTS.md` Section 6 is your only protection, so watch for direct commits.

### A6. Make the scratch test folder
Create `C:\wc-test\` and put in it a `photo.png`, a `song.wav`, a `doc.pdf`, and a `notes.txt`. **Never test drags on real files until Step 05 is approved.**

---

## Part B — The loop for every step

```
1. Builder agent:  branch → build → HANDOFF entry → push → open PR
2. You:            run the "User" tests from the task file
3. Reviewer agent: read PR + HANDOFF → APPROVE / CHANGES NEEDED
4. You:            merge PR on GitHub (only if approved and your tests passed)
5. You:            git checkout main && git pull   (before the next step)
```

**Rules for you:**
- Only one tool open on the repo at a time. Close Antigravity's agent before starting Claude Code, and vice versa.
- Always `git pull` on `main` before starting the next step.
- If an agent touches a file not listed in its task, or starts the next step on its own, stop it.

---

## Part C — Copy-paste prompts

### Start a BUILD step (either tool)
```
Read AGENTS.md, then docs/HANDOFF.md, then docs/tasks/step-0N-<name>.md.
You are the BUILDER for Step 0N. Create branch step-0N-<name> from main.
Restate the plan in 3–5 lines and wait for my "go".
```

### Start a REVIEW step (the other tool)
```
Read AGENTS.md, then docs/HANDOFF.md, then docs/tasks/step-0N-<name>.md.
You are the REVIEWER for Step 0N. Check out branch step-0N-<name>.
Review the diff against main using AGENTS.md Section 7. Do not rewrite code.
Append an APPROVE or CHANGES NEEDED entry to docs/HANDOFF.md, commit it to the same branch, and push.
```

### If the reviewer says CHANGES NEEDED (back to builder)
```
Read the latest REVIEW entry in docs/HANDOFF.md. Fix only the numbered findings on branch step-0N-<name>.
Append a new BUILD entry listing each finding and how it was fixed. Push. Stop.
```

### Where to open each tool
- **Claude Code:** in PowerShell, `cd "D:\Claude Base\Cowork Playground\APPS\wheelconvert"` then `claude`. It reads `CLAUDE.md` → `AGENTS.md` automatically.
- **Antigravity:** File → Open Folder → `D:\Claude Base\Cowork Playground\APPS\wheelconvert`. It reads `AGENTS.md` automatically. **Do not let it create `GEMINI.md`.**

---

## Part D — Step order

| Step | Builder | Reviewer | Your manual tests |
|---|---|---|---|
| 01 Skeleton | Antigravity | Claude Code | Run app, check log, run twice |
| 02 Hook | Claude Code | Antigravity | Shift-drag in Explorer / Notepad / desktop |
| 03 Wheel | Antigravity | Claude Code | Wheel appears, focus kept, Esc closes |
| 04 Drag | Antigravity | Claude Code | Labels per file type, copy cursor |
| 05 Drop | Claude Code | Antigravity | **Files never move** — check every time |
| 06 Hardening | Claude Code | You | All 10 acceptance tests + Defender scan |

After Step 06 merges: `git tag v0.0.1-spike && git push --tags`.

---

## Part E — When things go wrong
- **Agent edited `main` directly:** `git log main` to see it, then ask the agent to move the commit to a branch. Don't fix it yourself by force-pushing.
- **Two agents changed the same file:** you broke the one-tool-at-a-time rule. Keep the reviewer-approved version; discard the other.
- **Build fails after merging:** open a `fix-<short-name>` branch; the builder of the last step fixes it.
- **A file disappeared during a drag test:** stop. Check the Recycle Bin and the drop target folder. Report it as a guardrail-1 violation in HANDOFF.md before anything else.
