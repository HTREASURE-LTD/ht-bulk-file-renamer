; HT Bulk File Renamer - HTREASURE LTD branded installer (based on the template's HT-Product-Setup.iss).
; Run Build-Release.ps1 first: it publishes a self-contained win-x64 build (the .NET 10 runtime is included,
; so nothing else needs installing), then compile this file with Inno Setup 6.3 or later.
; Keep this file UTF-8 with BOM so the copyright symbol is preserved.
; Keep the AppId: it identifies this product for upgrades and uninstall.

#define ProductId      "bulk-file-renamer"
#define ProductName    "HT Bulk File Renamer"
#define ProductExe     "HTBulkFileRenamer.exe"
#define ProductVersion "0.7.1"
#define Publisher      "HTREASURE LTD"
#define CopyrightYear  GetDateTimeString('yyyy', '', '')
#define Copyright      "Copyright © " + CopyrightYear + " HTREASURE LTD. All rights reserved."
#define PublishDir     "..\HTBulkFileRenamer\bin\publish\win-x64"
#define BrandingRoot   "..\..\external\HT-Software-Template\HTREASURE-Branding"

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
; .NET 10 Windows build: 64-bit Windows 10 version 1809 (build 17763) or later, as in the template.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; End-User Licence Agreement page: the user must accept it before installing.
LicenseFile=..\..\LICENSE

; Branding: product icon for the setup .exe, HTREASURE wizard artwork at 100%, 150% and 200% scaling
SetupIconFile={#BrandingRoot}\Icons\Products\{#ProductId}\HT-{#ProductId}.ico
WizardStyle=modern
WizardImageFile=Images\inno-wizard-large-164x314.bmp,Images\inno-wizard-large-246x459.bmp,Images\inno-wizard-large-328x604.bmp
WizardSmallImageFile=Images\inno-wizard-small-55x55.bmp,Images\inno-wizard-small-83x80.bmp,Images\inno-wizard-small-110x106.bmp

; Code signing: Build-Release.ps1 defines SignTool (and passes the matching /Shtsign=... command) when HTREASURE LTD's
; certificate or Azure Artifact Signing is configured, so Setup and its uninstaller are signed. See docs\CODE-SIGNING.md.
#ifdef SignTool
SignTool={#SignTool}
SignedUninstaller=yes
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; The whole publish folder: app, .NET runtime, security\advisory-snapshot.json and ht-compatibility.json.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\{#ProductName}"; Filename: "{app}\{#ProductExe}"
Name: "{group}\Licence Agreement"; Filename: "{app}\LICENSE.txt"
Name: "{autodesktop}\{#ProductName}"; Filename: "{app}\{#ProductExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#ProductExe}"; Description: "{cm:LaunchProgram,{#StringChange(ProductName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
