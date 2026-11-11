; Build Release first, then compile this file with Inno Setup 6.
#define AppName "HT Bulk File Renamer Pro"
#define AppVersion "0.3.0"
#define ExePath "..\HTBulkFileRenamer\bin\Release\net462\HTBulkFileRenamer.exe"
[Setup]
AppId={{EFC9B9D2-28E8-48F5-88AB-3E8147D8A38B}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\HT Software\Bulk File Renamer Pro
DefaultGroupName=HT Software
OutputDir=Output
OutputBaseFilename=HTBulkFileRenamerPro_Setup_0.3.0
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x86 x64
UninstallDisplayIcon={app}\HTBulkFileRenamer.exe
[Files]
Source: "{#ExePath}"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\HT Bulk File Renamer Pro"; Filename: "{app}\HTBulkFileRenamer.exe"
Name: "{autodesktop}\HT Bulk File Renamer Pro"; Filename: "{app}\HTBulkFileRenamer.exe"; Tasks: desktopicon
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"
[Run]
Filename: "{app}\HTBulkFileRenamer.exe"; Description: "Launch HT Bulk File Renamer Pro"; Flags: nowait postinstall skipifsilent
