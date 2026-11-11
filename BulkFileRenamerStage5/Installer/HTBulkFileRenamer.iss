; HT Bulk File Renamer - HTREASURE LTD branded installer (based on the brand kit's HT-Product-Setup.iss).
; Build Release first (Build-Release.ps1), then compile this file with Inno Setup 6.3 or later.
; Keep this file UTF-8 with BOM so the copyright symbol is preserved.
; Keep the AppId: it identifies this product for upgrades and uninstall.

#define ProductId      "bulk-file-renamer"
#define ProductName    "HT Bulk File Renamer"
#define ProductExe     "HTBulkFileRenamer.exe"
#define ProductVersion "0.5.0"
#define Publisher      "HTREASURE LTD"
#define CopyrightYear  GetDateTimeString('yyyy', '', '')
#define Copyright      "Copyright © " + CopyrightYear + " HTREASURE LTD. All rights reserved."
#define ExePath        "..\HTBulkFileRenamer\bin\Release\net462\" + ProductExe

[Setup]
AppId={{EFC9B9D2-28E8-48F5-88AB-3E8147D8A38B}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppVerName={#ProductName} {#ProductVersion}
AppPublisher={#Publisher}
AppCopyright={#Copyright}
AppComments=Developed by HTREASURE LTD

VersionInfoVersion={#ProductVersion}
VersionInfoCompany={#Publisher}
VersionInfoCopyright={#Copyright}
VersionInfoProductName={#ProductName}
VersionInfoProductVersion={#ProductVersion}
VersionInfoDescription={#ProductName} Setup

DefaultDirName={autopf}\HTREASURE\{#ProductName}
DefaultGroupName=HTREASURE\{#ProductName}
UninstallDisplayName={#ProductName}
UninstallDisplayIcon={app}\{#ProductExe}
OutputDir=Output
OutputBaseFilename=HTBulkFileRenamer-{#ProductVersion}-Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x86 x64

; Branding: product icon for the setup .exe, HTREASURE wizard artwork at 100%, 150% and 200% scaling
SetupIconFile=..\HTBulkFileRenamer\Branding\HT-{#ProductId}.ico
WizardStyle=modern
WizardImageFile=Images\inno-wizard-large-164x314.bmp,Images\inno-wizard-large-246x459.bmp,Images\inno-wizard-large-328x604.bmp
WizardSmallImageFile=Images\inno-wizard-small-55x55.bmp,Images\inno-wizard-small-83x80.bmp,Images\inno-wizard-small-110x106.bmp

; Code signing: leave disabled until HTREASURE LTD has a real code-signing certificate.
; SignTool=htsign $f
; SignedUninstaller=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#ExePath}"; DestDir: "{app}"; Flags: ignoreversion
; Runtime settings (long path handling) live in the .exe.config next to the exe.
Source: "{#ExePath}.config"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#ProductName}"; Filename: "{app}\{#ProductExe}"
Name: "{autodesktop}\{#ProductName}"; Filename: "{app}\{#ProductExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#ProductExe}"; Description: "{cm:LaunchProgram,{#StringChange(ProductName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
