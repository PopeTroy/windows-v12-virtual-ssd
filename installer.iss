[Setup]
AppName=SovereignSSD Virtual SSD
AppVersion=12.0
DefaultDirName={autopf}\SovereignSSD
DefaultGroupName=SovereignSSD
OutputBaseFilename=SovereignSSD-Setup-v12
Compression=lzma2/ultra
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\SovereignSSD.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\sovereign_compressor.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\SovereignSSD"; Filename: "{app}\SovereignSSD.exe"
Name: "{autodesktop}\SovereignSSD"; Filename: "{app}\SovereignSSD.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SovereignSSD.exe"; Description: "{cm:LaunchProgram,SovereignSSD}"; Flags: nowait postinstall skipifsilent
