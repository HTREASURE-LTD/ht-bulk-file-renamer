# HT Bulk File Renamer Pro — Stage 3 (source release)

## What is included
- Windows Forms C# project targeting .NET Framework 4.6.2 (`net462`).
- Drag/drop, prefix/suffix, find/replace, case conversion, automatic numbering, live preview.
- File filtering (`*.txt;*.jpg`), optional recursive folder import (avoids directory junction loops).
- Save/load XML presets (`.htpreset`), append-only local rename/undo audit history, basic progress and cancel-before-commit.
- Staged rename with best-effort rollback on failure, one-level undo during current session.
- Inno Setup installer source script and build instructions.

## Build on Windows 11 with Visual Studio 2026
1. Extract ZIP to `C:\HTSoftware\BulkFileRenamerStage3` (do not overwrite Stage 2).
2. Open `HTBulkFileRenamer\HTBulkFileRenamer.csproj`.
3. Ensure **.NET desktop development** workload and .NET SDK are installed.
4. Restore NuGet packages; Build > Rebuild Solution; run Ctrl+F5.
5. If SDK resolution fails, run `where dotnet` and `dotnet --list-sdks` and verify the x64 SDK appears first.
6. Expected output: `HTBulkFileRenamer\bin\Release\net462\HTBulkFileRenamer.exe` after selecting Release and building.

## Feature notes and limitations
- Filter affects **newly added files only**; changing it does not remove files already in the list.
- Folder import adds top-level files by default; enable **Include subfolders** for recursive import.
- Numbering applies to the entire loaded list, ordered by file selection/import; uses `001_originalname.ext`.
- Preview checks collisions and common Windows naming restrictions. It is not a substitute for backing up data.
- Progress updates during staged moves. **Cancel** is honored during the staging phase; once commit begins, it completes the commit rather than interrupting half-renamed files.
- On failure, rollback is best-effort; inspect the folder for `.ht-renamer-*.tmp` if prompted.
- Undo is available only until the application closes or another rename succeeds. The persistent history is an **audit log**, not a persistent undo system.
- History file: `%LOCALAPPDATA%\HTSoftware\BulkFileRenamer\history.tsv`. Paths are base64 encoded for safe tab-delimited storage, **not encrypted**. Treat the history as potentially sensitive.
- No licence key/activation is included. Installer script is provided, but the installer executable is not prebuilt.
- No Windows 7/8/8.1/10 testing has been performed. Targeting `net462` does not guarantee compatibility with every environment.
- Windows 7/8/8.1 and .NET Framework 4.6.2 are out of support; security and commercial support implications must be evaluated before distribution.

## Test checklist
1. Build, launch, add disposable files and preview.
2. Numbering `1`, `3` digits yields `001_`, `002_`, `003_`.
3. Drag files, add folder, test filters and recursion.
4. Save preset, change settings, load preset and verify restoration.
5. Rename then Undo and confirm original filenames.
6. Open History and verify `RENAME` and `UNDO` entries.
7. Test duplicate destinations, invalid characters, and existing-file collisions.
8. Test a deliberately locked file and verify rollback behaviour.
9. Test cancel on a large disposable batch and verify no files are lost.
10. Test on actual target OS VMs before advertising compatibility.

## Installer
Install Inno Setup from https://jrsoftware.org/isinfo.php, build in Release mode, then compile `Installer\HTBulkFileRenamer.iss`. The installer requires .NET Framework 4.6.2 on the target PC; it does not install the runtime automatically.

## Licensing
This project is supplied as editable starter source for your own development. Check the licences of all NuGet dependencies, installer tooling, icons and other assets before commercial redistribution. Do not market this build as fully tested or production-ready without completing acceptance tests.
