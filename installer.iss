[Setup]
AppName=SovereignSSD
AppVersion=12.0
DefaultDirName={autopf}\SovereignSSD
DefaultGroupName=SovereignSSD
OutputDir=Output
OutputBaseFilename=SovereignSSD-Setup-v12
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin

#ifndef SourcePath
  #define SourcePath "publish\SovereignSSD.exe"
#endif

[Files]
Source: "{#SourcePath}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\SovereignSSD"; Filename: "{app}\SovereignSSD.exe"
Name: "{autodesktop}\SovereignSSD"; Filename: "{app}\SovereignSSD.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Run]
Filename: "{app}\SovereignSSD.exe"; Description: "{cm:LaunchProgram,SovereignSSD}"; Flags: postinstall shellexec skipifsilent
