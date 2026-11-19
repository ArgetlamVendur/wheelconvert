# Morning checklist — after run 2 (2026-11-19)

Build `main` (`dotnet build WheelConvert.sln -c Release`) and start the app as usual. Use only files in `D:\Claude Base\Cowork Playground\APPS\wc-test\`. Highest risk first.

| # | WP | Test | Steps | Expected | Result |
|---|---|---|---|---|---|
| 1 | 14 (test 9) | **PDF wheel hit test (10 segments)** | Shift-drag 1 PDF, drop on **Reverse**, 5 times; then **Rotate** 5 times; then select 3 PDFs, Shift-drag, drop on **Merge** 5 times | 15/15 land on the intended segment. If fewer, write down which missed (an ADR will move the least-used PDF targets) | |
| 2 | 14 (test 7) | Merge | Select 3 PDFs (e.g. `doc 10.pdf`, `doc 2.pdf`, `a.pdf`), Shift-drag, drop on **Merge** | One `<first> (merged).pdf` next to the first file; pages in natural name order (2 before 10); opens in Edge; sources unchanged | |
| 3 | 14 (test 8) | No Merge for one PDF | Shift-drag a single PDF | No **Merge** segment | |
| 4 | 15 (test 8) | Images → PDF | Select 3 phone photos (one HEIC, one sideways), Shift-drag, drop on **PDF** | One `<first> (combined).pdf`: A4 pages, upright, in name order; wide photos on landscape pages | |
| 5 | 15 | One image → PDF | Shift-drag one JPEG, drop on **PDF** | `photo (converted).pdf`, one A4 page | |
| 6 | 15 (test 9) | Portal upload | Upload the PDF from step 4 to a portal you use | Accepted | |
| 7 | 13 (tests 9–10) | Multi-file wheel | Drag 2 PDFs; then drag 1 PDF + 1 JPG | 2 PDFs: Split, Halves, Odd, Even, Reverse and Reorder are hidden. PDF + JPG: no Merge | |
| 8 | log chore | Log has no folders | Rename a `.txt` copy to `.jpg`, drop it on **PNG**, open `%LOCALAPPDATA%\WheelConvert\wheelconvert.log` | Last line names only the file, no folder path | |

| 9 | 16 (rows 3, 9) | **HEIC + Smaller (PR #34, before merging it)** | Check out branch `wp-16-smaller`, build, run. Shift-drag an iPhone HEIC photo, drop on **Smaller**; then a phone video, drop on **Smaller** | `photo (smaller).jpg`, upright, much smaller; `clip (smaller).mp4`, short edge ≤ 720, plays in Photos. If both pass, merge #34 | |

Not testable yet (not merged): WP-18 PDF → Word (#31; needs the licence files, steps in `docs/reviews/WP-18-2026-11-19-rereview.md`), WP-20 Smaller PDF (#32, BLOCKED after 2 fix rounds), WP-19.
