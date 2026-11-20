# HT Bulk File Renamer — Stage 4 (source release)

An HTREASURE LTD product. Copyright © 2026 HTREASURE LTD. All rights reserved.

## What is included
- Windows Forms C# project targeting .NET Framework 4.6.2 (`net462`).
- Drag/drop, prefix/suffix, find/replace, case conversion, automatic numbering, live preview.
- File filtering (`*.txt;*.jpg`), optional recursive folder import (avoids directory junction loops).
- Save/load XML presets (`.htpreset`), rename/undo history, progress and cancel-before-commit.
- Staged rename with best-effort rollback on failure.
- Inno Setup installer source script and build instructions.

### New in Stage 4
- **Choose and order files.** Each row has an **Include** checkbox; excluded files keep their name and don't use a number. Reorder with **Move Up / Move Down** (or Alt+Up / Alt+Down), or click the **Original filename** (natural order: 2 before 10) or **Modified** header to sort. Right-click for include/exclude all. Space toggles the selected rows; Delete removes them from the list.
- **Find options.** **Match case** (on by default, as in Stage 3) and **Regular expression** (.NET syntax; use `$1`, `$2` in Replace). Invalid patterns show in the status bar.
- **Name tokens** in Prefix and Suffix: `{n}` number, `{date}` modified date, `{created}` created date, `{parent}` folder name. Dates take a format, e.g. `{date:yyyyMMdd}`. Using `{n}` replaces the automatic `001_` position.
- **Undo from History, even after a restart.** **History** opens a window listing every batch with its files; **Undo Selected Batch** restores a rename batch, checked by the same rules as a rename.
- **Remembers settings** (all rename options plus window size and position) between runs.
- Fixes from the Stage 3 review: path separators in prefix/suffix/replace are blocked, all controls lock during a rename, the window can't close mid-rename, and history-save failures are shown.

### HTREASURE branding
Applied from the HTREASURE LTD brand kit (`C:\HTSoftware\Htreasure_files\HTREASURE-Branding`, `brand.json` v1.0.0), following its WinForms sample and the guidelines' "Software interface branding" page:
- Product name **HT Bulk File Renamer** (was "HT Bulk File Renamer Pro") and the kit's product icon for the executable, title bar and header.
- Deep Navy header with icon, name and description; brand colours throughout — Electric Blue only for the primary action (Rename Files, Undo Selected Batch), Technology Blue for secondary buttons and selected rows, Edge for neutral buttons.
- Status bar with `© {year} HTREASURE LTD · Developed by HTREASURE LTD`; **Help > About** shows the standard HTREASURE About box (logo, version, copyright, publisher and company number).
- File properties: Company `HTREASURE LTD`, Product, Description, Version and Copyright come from the `.csproj`. The copyright year is the build year, so it never goes stale.
- Installer: publisher HTREASURE LTD, product icon, HTREASURE wizard artwork, installs to `Program Files\HTREASURE\HT Bulk File Renamer` (existing installs upgrade in place: the AppId is unchanged).
- The kit's ready-made `HTREASURE.Branding.WinForms` library needs .NET 10, so this .NET Framework 4.6.2 app applies the same rules in `Brand.cs` and `AboutForm.cs`. The assets it uses are copied into `HTBulkFileRenamer\Branding\` and embedded in the exe; the wizard images are in `Installer\Images\`. If the brand kit changes, copy the updated files over these.
- Not added: the splash screen (the app opens instantly, and the guidelines say a splash should only last as long as loading takes) and the Inter font (the guidelines specify Segoe UI for desktop application UI).
- The executable is **not** code-signed; don't describe it as signed until a certificate is configured.

### Source layout
- `Program.cs` — entry point.
- `MainForm.cs` — main window, file list, presets, rename/undo execution.
- `RenameEngine.cs` — naming rules (find/replace, tokens, numbering) and validation; no UI.
- `RenameHistory.cs` — reading/writing `history.tsv` and grouping it into batches.
- `HistoryForm.cs` — History window.
- `Brand.cs`, `AboutForm.cs`, `Branding\` — HTREASURE colours, company details, icon/logo and the About box.

## Build on Windows 11 with Visual Studio 2026
1. Open `HTBulkFileRenamer\HTBulkFileRenamer.csproj`.
2. Ensure **.NET desktop development** workload and .NET SDK are installed.
3. Restore NuGet packages; Build > Rebuild Solution; run Ctrl+F5.
4. If SDK resolution fails, run `where dotnet` and `dotnet --list-sdks` and verify the x64 SDK appears first.
5. Expected output: `HTBulkFileRenamer\bin\Release\net462\HTBulkFileRenamer.exe` after selecting Release and building (or run `Build-Release.ps1`).

## Feature notes and limitations
- Filter affects **newly added files only**; changing it does not remove files already in the list.
- Folder import adds top-level files by default; enable **Include subfolders** for recursive import.
- Numbering follows the list order and counts only included files.
- Date tokens use the dates read when the file was added to the list.
- Preview checks collisions and common Windows naming restrictions. It is not a substitute for backing up data.
- Progress updates during staged moves. **Cancel** is honored during the staging phase; once commit begins, it completes the commit rather than interrupting half-renamed files.
- On failure, rollback is best-effort; inspect the folder for `.ht-renamer-*.tmp` if prompted.
- History undo fails safely (nothing is moved) if any file in the batch has since been moved, renamed or deleted, or if an original name is now taken.
- Data folder: `%LOCALAPPDATA%\HTSoftware\BulkFileRenamer\` — `history.tsv` and `settings.xml`. History paths are base64 encoded for safe tab-delimited storage, **not encrypted**. Treat the history as potentially sensitive.
- `history.tsv` now has two extra columns (batch id, undone batch id). Stage 3 entries are still read: their batches are grouped by time, and a Stage 3 undo is matched to the rename it reversed.
- Stage 3 `.htpreset` files load unchanged (Match case on, Regular expression off).
- No licence key/activation is included. Installer script is provided, but the installer executable is not prebuilt.
- No Windows 7/8/8.1/10 testing has been performed. Targeting `net462` does not guarantee compatibility with every environment.
- Windows 7/8/8.1 and .NET Framework 4.6.2 are out of support; security and commercial support implications must be evaluated before distribution.

## Test checklist
1. Build, launch, add disposable files and preview.
2. Numbering `1`, `3` digits yields `001_`, `002_`, `003_`.
3. Untick a row: it shows **Excluded** and the numbers close up. Move rows and sort by name/date; numbers follow the order.
4. Find `report` / Replace `summary` with Match case off; then Regular expression `^(\d+)(.*)$` → `$2_$1`.
5. Prefix `{date:yyyyMMdd}_`, Suffix `_{n}`, and `{parent}-`; check the preview.
6. Prefix `..\` and `{date:yyyy/MM}` must be refused with a clear message.
7. Drag files, add folder, test filters and recursion.
8. Save preset, change settings, load preset and verify restoration; load a Stage 3 preset.
9. Close and reopen the app: settings and window size are restored.
10. Rename, close the app, reopen, **History** → select the batch → **Undo Selected Batch**; confirm original names and the batch shows **Undone**.
11. Test duplicate destinations, invalid characters, and existing-file collisions.
12. Test a deliberately locked file and verify rollback behaviour.
13. Test cancel on a large disposable batch and verify no files are lost.
14. Test on actual target OS VMs before advertising compatibility.

## Installer
Install Inno Setup from https://jrsoftware.org/isinfo.php, build in Release mode, then compile `Installer\HTBulkFileRenamer.iss` (version 0.4.0). The installer requires .NET Framework 4.6.2 on the target PC; it does not install the runtime automatically.

## Licensing
This project is supplied as editable starter source for your own development. Check the licences of all NuGet dependencies, installer tooling, icons and other assets before commercial redistribution. Do not market this build as fully tested or production-ready without completing acceptance tests.
