HT Bulk File Renamer 0.6.0 adopts the HTREASURE design template used by HT ISO Ripper.

## What's new
- **Same look and layout as HT ISO Ripper**: blue header with the product icon and website link, menu bar below it, settings in captioned group boxes (Name Rules; Numbering, Case and Extension; Files), standard Windows buttons, and one primary button.
- **Bottom action row**: Rename Files, Cancel, Undo Last Rename, Close. Add Files…, Add Folder… and Clear List are in the Files group. All menus and keyboard shortcuts are unchanged.
- **Status bar** coloured by state (warnings amber, errors red, success green), with file / included / to-rename counts, a progress bar while renaming, and the company attribution.
- **View > Theme**: Same as Windows (default; follows a light/dark switch while the app is open), Light or Dark. Remembered between runs.
- About box and History window restyled to match.

## Fixes
- Warnings about folders that could not be read were immediately hidden by the preview message; they now stay visible.
- When every file is excluded, the status now says so instead of asking for a rename rule.

## Downloads
- `HTBulkFileRenamer-0.6.0-Setup.exe` — installer (needs .NET Framework 4.6.2 or later; requires administrator rights). Upgrades earlier installs in place.
- `HTBulkFileRenamer-0.6.0-Portable.zip` — no install: unzip and run `HTBulkFileRenamer.exe`. Keep `HTBulkFileRenamer.exe.config` next to it.
- `SHA256SUMS.txt` — checksums for both files.

## Known limitations
- **Not code-signed.** Windows SmartScreen may warn before running the installer or the exe.
- Pre-release: tested with the automated suite (24 unit tests, window checks) on Windows 11 only. The installer was compiled but not run through an install/upgrade/uninstall test, and other Windows versions are untested.
- Saved settings and rename history stay in `%LOCALAPPDATA%\HTSoftware\BulkFileRenamer`.

Copyright © 2026 HTREASURE LTD. All rights reserved.
