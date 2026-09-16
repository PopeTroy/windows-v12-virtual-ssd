; ============================================================================
; VSSDHX V12 VIRTUAL SSD & SHINOBI BRAIN ENGINE - INNO SETUP CONFIGURATION
; ============================================================================

#define MyAppName "VSSDHX Sovereign Engine"
#define MyAppVersion "144.000.6000"
#define MyAppPublisher "Celsius Media Group"
#define MyAppURL "https://celsiustechmediagroup.co.za"
#define MyAppExeName "main.exe"

[Setup]
AppId={{A8F9E23B-991A-4C2E-842A-18456429381A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\VSSDHX_Sovereign_Engine
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=VSSDHX_Setup_v144
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
WizardStyle=modern

; Custom Branding Images
SetupIconFile=vssdhx_logo.ico
WizardImageFile=setup_banner.bmp
WizardSmallImageFile=vssdhx_logo_small.bmp

#ifndef SourcePath
  #define SourcePath "dist\main.exe"
#endif

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourcePath}"; DestDir: "{app}"; Flags: ignoreversion
Source: "onnx_cuda_tuner.py"; DestDir: "{app}"; Flags: ignoreversion
Source: "onnx_tuner.py"; DestDir: "{app}"; Flags: ignoreversion
Source: "vssdhx_dlss5_config.ini"; DestDir: "{app}"; Flags: ignoreversion
Source: "Shinobi_HPL3_DIP_Enhancer.fx"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\vssdhx_logo.ico"
Name: "{group}\VSSDHX ONNX Brain Prompt"; Filename: "cmd.exe"; Parameters: "/K python ""{app}\onnx_tuner.py"" --interactive"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\vssdhx_logo.ico"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: postinstall shellexec skipifsilent
