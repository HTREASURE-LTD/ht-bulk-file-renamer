# HT Bulk File Renamer — version 0.7.1 (source release)

An HTREASURE LTD product. Copyright © 2026 HTREASURE LTD. All rights reserved.

## What is included
- Windows Forms C# project targeting **.NET 10** (`net10.0-windows`), built on the HTREASURE application template
  ([HT-Software-Template](https://github.com/HTREASURE-LTD/HT-Software-Template)) and meeting the HTREASURE
  [software standards](https://github.com/HTREASURE-LTD/ht-software-standards).
- Drag/drop, prefix/suffix, find/replace, case conversion, automatic numbering, live preview.
- File filtering (`*.txt;*.jpg`), optional recursive folder import (avoids directory junction loops).
- Save/load XML presets (`.htpreset`), rename/undo history, progress and cancel-before-commit.
- Staged rename with best-effort rollback on failure.
- Security Advisory History (mandatory HTREASURE standard) and the HTREASURE Software Update Service hook.
- Inno Setup installer source script and build instructions.

### New in 0.7.1 — Code signing in the release build
- `Build-Release.ps1` builds the whole release (tests, self-contained publish, installer, portable zip, checksums) and Authenticode-signs HTREASURE's own binaries, Setup and its uninstaller when a certificate or Azure Artifact Signing is configured, then verifies every signature. See [docs/CODE-SIGNING.md](docs/CODE-SIGNING.md). The app itself is unchanged from 0.7.0.

### New in 0.7.0 — HTREASURE template and software standards
- **.NET 10.** The app and tests target `net10.0-windows`. Requirements: **64-bit Windows 10 version 1809 (build 17763) or later**; Windows 7, 8 and 8.1 are no longer supported. The installer and portable zip are self-contained, so users don't need to install .NET.
- **Template build files.** `Directory.Build.props/.targets` import the template's branding build files and the standards build targets. Company, copyright year and version metadata, the executable icon (`<HTProductId>bulk-file-renamer</HTProductId>`) and the embedded product icon come from the template; the local copies (`Brand.cs`, `Branding\`) were removed in favour of the kit's `HTREASURE.Branding.WinForms` library (`BrandInfo`, `BrandWinForms`).
- **Splash screen** (`BrandSplashForm`), shown only while the main window is built, as in the template. High-DPI mode `PerMonitorV2`; every window scales its 96-DPI layout.
- **Security Advisory History** (mandatory): **Help → About → Security Advisory History** and **Help → Security Advisories** (`HTREASURE.Standards.WinForms`). Every build bundles an offline advisory snapshot (`security\advisory-snapshot.json`) and writes `ht-compatibility.json` (app id `ht-bulk-file-renamer`, channel `stable`, platform `windows-x64`, minimum OS 10.0.17763). A background advisory check runs at start-up once an advisory service is configured.
- **Help → Check for Updates** uses the HTREASURE Software Update Service once a signed release feed is configured (`<HTReleaseFeedUrl>`); until then it says so. Nothing is downloaded or installed without approval.
- **Tools → Open Data Folder** (settings, presets, rename history), as in the template's Tools menu.
- Long paths: .NET 10 handles paths of 260+ characters itself, so they are allowed without Windows' `LongPathsEnabled` setting.
- The template and standards repositories are **git submodules** in `external/` (see Build).

### New in 0.6.1 — End-User Licence Agreement
- HTREASURE LTD End-User Licence Agreement (`LICENSE`): accepted on the installer's licence page, installed as `LICENSE.txt` with a Start-menu shortcut, shown in **Help > Licence Agreement** and linked from **About**, and included in the portable zip. See "Licensing" below.

### New in 0.6.0 — HTREASURE design template (shared with HT ISO Ripper)
- **Same look and layout as HT ISO Ripper**: blue header banner (product icon, name, description, `www.htreasure.com` link), menu bar below it, settings in captioned group boxes with a label column (**Name Rules**; **Numbering, Case and Extension**; **Files**), standard Windows buttons, and one accent-coloured primary button.
- **Bottom action row** like the ISO Ripper: **Rename Files** (primary), **Cancel**, **Undo Last Rename**, **Close**. **Add Files…**, **Add Folder…** and **Clear List** sit in the Files group. The menus and shortcuts are unchanged.
- **Status bar** coloured by state (warnings amber, errors red, success green), with file / included / to-rename counts, a progress bar while renaming, and the company attribution.
- **View > Theme**: Same as Windows (default; follows a light/dark switch while the app is open), Light or Dark, with a dark title bar on Windows 10 2004+ and 11. Remembered between runs.
- About box and History window restyled to match; the About box uses the brand kit's dark-background logo in dark mode.
- Fixes found in review: warnings about unreadable folders were immediately hidden by the preview message; the status now says so when every file is excluded.
- `ThemeManager.cs` is ported from HT ISO Ripper (`src/HTISORipper.UI/ThemeManager.cs`) and extended for the file grid and right-click menu.

### New in Stage 5
- **Menus instead of the button bar**, giving the file list more room:
  - **File**: Add Files (Ctrl+O), Add Folder (Ctrl+Shift+O), Load/Save Preset (Ctrl+S), Export Preview to CSV (Ctrl+E), Exit.
  - **List**: Remove Selected (Del), Clear List, Move Up/Down (Alt+Up/Down), Include/Exclude All.
  - **Rename**: Refresh Preview (F5), **Rename Files (Ctrl+R)**, Undo Last Rename, Cancel Rename, History (Ctrl+H).

  Del and Alt+arrows act on the file list only, and Undo has no Ctrl+Z, so text boxes keep their normal keys. While a rename runs, every command except Cancel Rename is disabled.
- **Extension options.** **Extension**: Keep original, lowercase, UPPERCASE, or Replace with… a **New extension** (with or without the dot; leave it empty to remove the extension). Find/replace, tokens and filename case still never touch the extension.
- **`{taken}` token**: the photo's "date taken" from EXIF (JPEG/TIFF), e.g. `{taken:yyyyMMdd}_`. Files without one use their modified date and show **Pending (no photo date)**. Photo dates are read the first time `{taken}` is used, so a large list may pause briefly then.
- **Export CSV** saves the preview (include, folder, original name, proposed name, modified, status) as UTF-8 CSV for Excel.
- **Automated tests.** `HTBulkFileRenamer.Tests` (MSTest, 24 tests) covers naming rules, validation, history, EXIF reading and the real file moves: swaps, three-way cycles, case-only renames, cancel, and rollback when a file is locked. The file-moving code moved out of the window into `RenameExecutor.cs` so it can be tested.
- Presets and saved settings gain `ExtensionMode`/`NewExtension`; older presets load with the extension kept.

### New in Stage 4
- **Choose and order files.** Each row has an **Include** checkbox; excluded files keep their name and don't use a number. Reorder with **Move Up / Move Down** (or Alt+Up / Alt+Down), or click the **Original filename** (natural order: 2 before 10) or **Modified** header to sort. Right-click for include/exclude all. Space toggles the selected rows; Delete removes them from the list.
- **Find options.** **Match case** (on by default, as in Stage 3) and **Regular expression** (.NET syntax; use `$1`, `$2` in Replace). Invalid patterns show in the status bar.
- **Name tokens** in Prefix and Suffix: `{n}` number, `{date}` modified date, `{created}` created date, `{parent}` folder name. Dates take a format, e.g. `{date:yyyyMMdd}`. Using `{n}` replaces the automatic `001_` position.
- **Undo from History, even after a restart.** **History** opens a window listing every batch with its files; **Undo Selected Batch** restores a rename batch, checked by the same rules as a rename.
- **Remembers settings** (all rename options plus window size and position) between runs.
- Fixes from the Stage 3 review: path separators in prefix/suffix/replace are blocked, all controls lock during a rename, the window can't close mid-rename, and history-save failures are shown.

### HTREASURE branding
Company identity and assets come from the brand kit in the template submodule (`external/HT-Software-Template/HTREASURE-Branding`, `brand.json`); colours and layout follow the HTREASURE design template used by HT ISO Ripper (see "New in 0.6.0"):
- Product name **HT Bulk File Renamer** and the kit's product icon for the executable, title bar, header and installer.
- Status bar with the kit's attribution (`© {year} HTREASURE LTD · …`); **Help → About → About this application** shows the HTREASURE About box (logo, version and runtime, copyright, publisher, company number, website and licence agreement). The office address is deliberately not shown.
- File properties: Company `HTREASURE LTD`, Product, Description, Version and Copyright. The copyright year is the build year, so it never goes stale.
- Installer: publisher HTREASURE LTD, product icon, HTREASURE wizard artwork, installs to `Program Files\HTREASURE\HT Bulk File Renamer` (the AppId is unchanged, so it upgrades earlier installs).
- The executable is **not** code-signed; don't describe it as signed until a certificate is configured.

### Source layout
- `HTBulkFileRenamer.slnx` — solution with the app and the tests.
- `Directory.Build.props`, `Directory.Build.targets` — HTREASURE template and standards build integration.
- `Program.cs` — entry point and splash screen.
- `MainForm.cs` — main window, file list, presets, export, rename/undo commands, standards menus.
- `RenameEngine.cs` — naming rules (find/replace, tokens, numbering, extension), photo dates, validation, CSV; no UI.
- `RenameExecutor.cs` — staged file moves with rollback; no UI.
- `RenameHistory.cs` — reading/writing `history.tsv` and grouping it into batches.
- `HistoryForm.cs` — History window.
- `ThemeManager.cs` — HTREASURE design template: light/dark palettes and control styling (from HT ISO Ripper).
- `AboutForm.cs`, `LicenceForm.cs` — About box and licence agreement.
- `app.manifest` — Windows compatibility and long-path declaration.
- `HTBulkFileRenamer.Tests\` — automated tests.
- `..\external\` — submodules: `HT-Software-Template` (brand kit and branding libraries) and `ht-software-standards` (Security Advisory History, update service, build targets).

## Build on Windows 11 with Visual Studio 2026
1. Clone with submodules: `git clone --recurse-submodules https://github.com/HTREASURE-LTD/ht-bulk-file-renamer.git` (or `git submodule update --init` in an existing clone). Both submodules are private HTREASURE-LTD repositories.
2. Install the **.NET 10 SDK** (10.0.100 or later) and Visual Studio 2026 with the **.NET desktop development** workload.
3. Open `HTBulkFileRenamer.slnx`; Build > Rebuild Solution; run Ctrl+F5. Run tests with Test > Run All Tests (or `dotnet test HTBulkFileRenamer.slnx`).
4. `Build-Release.ps1` builds the whole release into `Installer\Output\v<version>`: it runs the tests and, only if they pass, publishes a self-contained win-x64 build (about 120 MB, includes the .NET runtime), signs it when code signing is configured, compiles the installer, builds the portable zip (with `LICENSE.txt`), verifies signatures and writes `SHA256SUMS.txt`. Needs Inno Setup 6.3+.
   - **Code signing (Authenticode).** Set the `HT_SIGN_THUMBPRINT`/`HT_TIMESTAMP_URL` (certificate authority) or `HT_ARTIFACT_SIGNING_*` (Azure Artifact Signing) environment variables, the same as HT ISO Ripper. Without them the release is **unsigned** and the script warns. See [docs/CODE-SIGNING.md](docs/CODE-SIGNING.md), including the eligibility check for Artifact Signing.
   - **Advisory snapshot signing (standards requirement).** Release builds must sign the bundled advisory snapshot: `.\Build-Release.ps1 -SigningKey D:\offline\advisory.pem -KeyId <key id>`. HTREASURE has not provisioned a production signing key yet (`ht-software-standards/security/keys/trusted-keys.json` is empty), so without `-SigningKey` the script builds an **internal** release (`HTRequireSignedSnapshot=false`), and Security Advisory History shows "Development build: the bundled advisory snapshot is not signed".
   - The script sets the runtime identifier inside the `.csproj` (`HTSelfContainedRelease`), not on the `dotnet publish` command line, because command-line `-r`/`--self-contained` also reach the standards CLI build and break `HTREASURE.Standards.targets`.
5. When the advisory service and release feed exist, set `<HTAdvisoryServiceUrl>` and `<HTReleaseFeedUrl>` in `HTBulkFileRenamer.csproj`.

## Feature notes and limitations
- Filter affects **newly added files only**; changing it does not remove files already in the list.
- Folder import adds top-level files by default; enable **Include subfolders** for recursive import.
- Numbering follows the list order and counts only included files.
- Date tokens use the dates read when the file was added to the list.
- Preview checks collisions and common Windows naming restrictions. It is not a substitute for backing up data.
- Progress updates during staged moves. **Cancel** is honored during the staging phase; once commit begins, it completes the commit rather than interrupting half-renamed files.
- On failure, rollback is best-effort; inspect the folder for `.ht-renamer-*.tmp` if prompted.
- History undo fails safely (nothing is moved) if any file in the batch has since been moved, renamed or deleted, or if an original name is now taken.
- Data folder: `%LOCALAPPDATA%\HTSoftware\BulkFileRenamer\` — `history.tsv` and `settings.xml` (kept from earlier versions so history and settings carry over). The advisory cache lives in `%LOCALAPPDATA%\HTREASURE\ht-bulk-file-renamer\security`. History paths are base64 encoded for safe tab-delimited storage, **not encrypted**. Treat the history as potentially sensitive.
- `history.tsv` has two extra columns since Stage 4 (batch id, undone batch id). Stage 3 entries are still read.
- Stage 3 `.htpreset` files load unchanged (Match case on, Regular expression off).
- No licence key/activation is included.
- Only Windows 11 x64 has been used for testing. Windows 10 1809+ and ARM64 (x64 emulation) are untested.

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
15. Extension: lowercase on `.JPG` files; Replace with `md`; Replace with empty (extension removed).
16. Prefix `{taken:yyyyMMdd}_` on camera photos and on non-photos (shows **Pending (no photo date)**).
17. Export CSV and open it in Excel; check non-English filenames.
18. Rename a file in a folder deeper than 260 characters.
19. **Help → Security Advisories** and **Help → About → Security Advisory History**: the banner says the data is the bundled offline snapshot; **Export Security Report…** saves a report.
20. Run at 150% and 200% display scaling: windows and text scale cleanly.
21. Install over 0.6.x and check the upgrade, the licence page and uninstall.

## Installer
Install Inno Setup 6.3+ from https://jrsoftware.org/isinfo.php and run `Build-Release.ps1`, which compiles `Installer\HTBulkFileRenamer.iss` (version 0.7.1) and signs Setup and its uninstaller when code signing is configured. Setup installs the self-contained publish folder (no separate .NET install) and needs 64-bit Windows 10 version 1809 or later.

## Licensing
**End-User Licence Agreement:** `LICENSE` at the repository root (HTREASURE LTD EULA v1.0, based on the HT ISO Ripper agreement: free for personal, non-commercial use; other use needs a written licence). It is the single source for every copy:
- the installer's licence page, which must be accepted before installing, plus `LICENSE.txt` in the install folder and a Start-menu shortcut;
- **Help > Licence Agreement** and the link in **About**, read from a copy embedded in the exe;
- `LICENSE.txt` in the portable zip.

The self-contained build includes the Microsoft .NET runtime (MIT licence) and the Inter font (SIL Open Font Licence, via the branding library). Check the licences of all dependencies and assets before commercial redistribution. Do not market this build as fully tested or production-ready without completing acceptance tests.
