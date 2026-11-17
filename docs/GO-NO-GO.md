# Go / no-go: antivirus and signing (WP-12)

Status: **TEMPLATE. Nothing on this page has been scanned yet.** The results table is empty on purpose; the user fills it in. This page answers ADR-006: "does the Shift-drag mouse hook get flagged?"

## 1. What to scan
The file that real users run: the installed `WheelConvert.exe`.

1. Install a release candidate (`WheelConvert-Setup-<version>.exe` from the draft release) on a Windows user account.
2. The installed path is `%LOCALAPPDATA%\Programs\WheelConvert\WheelConvert.exe` (usually `C:\Users\<you>\AppData\Local\Programs\WheelConvert\WheelConvert.exe`).
3. Also scan the installer itself and the portable zip (`WheelConvert-<version>-win-x64-portable.zip`).

Check first that the SHA-256 of the files matches `SHA256SUMS.txt` from the release, so you know you scan what was published.

## 2. How
- **Microsoft Defender (required).** In File Explorer, right-click the file, choose "Scan with Microsoft Defender". Note the result. Then start the app, hold Shift and drag a file, and watch for any Defender or SmartScreen pop-up in the first minutes of use.
- **VirusTotal (only if you decide to).** Uploading shares the file with third parties and makes the hash public. Agents must not do this. If you choose to, upload the installer or exe yourself at virustotal.com and record the detection count. Looking up only the SHA-256 (no upload) is the lower-exposure option.
- Record every detection with the engine name and the detection name. One or two generic "heuristic" hits from small engines are common for unsigned apps; note them but do not treat them as a block.

## 3. Results (to be filled in by the user)

| Date | File (name + SHA-256 first 12 chars) | Tool | Result (clean / detections) | Engine and detection names | Hook flagged? (yes / no / unclear) | Notes |
|---|---|---|---|---|---|---|
| | | | | | | |
| | | | | | | |
| | | | | | | |

Decision: **GO** / **NO-GO** (user): ________  Date: ________

## 4. The ADR-006 rule
If the Shift-drag hook is flagged (Defender quarantines or blocks the app, or several mainstream engines name the mouse hook or keylogger behaviour), the agent proposes switching the **default trigger to Hotkey** through a **new ADR** that supersedes ADR-006's default. The user decides. The Shift-drag trigger stays available as a setting. Nothing is changed in code before that ADR is accepted.

If only generic detections appear and Defender is clean, record GO and keep Shift-drag as the default.

## 5. Signing options
Ship **unsigned, with `SHA256SUMS.txt`**, until the user approves one of these. Unsigned files trigger SmartScreen warnings until they build reputation; signing reduces that but does not guarantee it.

### SignPath Foundation (free for open source)
Source: <https://signpath.org/terms>, read 2026-11-17. The page is marked as a draft. Conditions it lists: an OSI-approved licence for all components with no commercial dual-licensing; no malware or potentially unwanted programs; no proprietary components (system libraries allowed); the project is actively maintained and already released in the form to be signed; functionality is documented on the download page; binaries are built from source in a verifiable, automated way from the source repository; each release is approved by a trusted team member before signing; defined Authors, Reviewers and Approvers; multi-factor authentication for all team members; the project home page has a "Code signing policy" section.
- Fits: MIT licence, GitHub Actions build, public repository (once public).
- Unclear: the page does not say whether a single individual can fill the separate Reviewer and Approver roles. Ask SignPath before applying. The Magick.Native LGPL question ([`THIRD-PARTY-NOTICES.md`](../THIRD-PARTY-NOTICES.md)) is not a SignPath condition on its own, but the "OSI-approved licence for all components" wording should be checked against the bundled LGPL/MPL libraries.

### Microsoft Artifact Signing (formerly Azure Trusted Signing)
Sources, read 2026-11-17: <https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart> (page dated 2026-05-21, updated 2026-10-08) and <https://learn.microsoft.com/en-us/azure/artifact-signing/faq> (dated 2026-05-14, updated 2026-10-27).
- Eligibility, quoted from the quickstart: "Public Trust certificates are available to organizations in the United States, Canada, the European Union, the United Kingdom, Australia, New Zealand, Japan, South Korea, Singapore, Switzerland, Norway, and Israel. Individual developers must be located in the United States or Canada."
- **India is not in either list, so an individual developer in India is not eligible for Public Trust certificates as the documentation reads today.** An organization registered in India is not listed either. Private Trust certificates are exempt from the geographic limit but are not publicly trusted, so they do not help with SmartScreen for other users.
- It also needs a paid Azure subscription (free, trial and sponsored subscriptions are refused) and does not issue EV certificates (FAQ).
- The documentation may change; recheck before relying on this. This is the documentation's wording, not a confirmation from Microsoft that an application would be refused.

### Other routes (not researched here)
A commercial code-signing certificate from a certificate authority that serves India, or signing through a legal entity in a supported country. Not evaluated.

## 6. Not done by agents
No file has been uploaded to VirusTotal or any other service, no installer has been run, and no Defender scan has been performed by the agent that wrote this page.
