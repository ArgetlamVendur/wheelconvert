; WheelConvert installer (WP-12). Per-user, no administrator rights.
; Build:  iscc /DAppVersion=0.1.0 /DSourceDir=<folder with WheelConvert.exe + licenses\> /DOutputDir=<folder> installer\WheelConvert.iss
; The release workflow (.github/workflows/release.yml) passes these values.

#ifndef AppVersion
  #define AppVersion "0.0.0-local"
#endif
#ifndef AppVersionNumeric
  #define AppVersionNumeric "0.0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist-installer"
#endif

#define AppName "WheelConvert"
#define AppExe "WheelConvert.exe"
; Must equal StartupRegistration.RunKeyPath / DefaultValueName in src\WheelConvert.App\Tray\StartupRegistration.cs.
#define RunKeyPath "Software\Microsoft\Windows\CurrentVersion\Run"
#define RunValueName "WheelConvert"

[Setup]
; Fixed GUID: lets a newer installer upgrade an older install. Never change it.
AppId={{B6C1E7A2-5D3F-4E8B-9A41-7F2C0D9E3A56}
AppName={#AppName}
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersionNumeric}
AppPublisher={#AppName}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; No admin: installs under the user's profile and writes only HKCU.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=WheelConvert-Setup-{#AppVersion}
SetupIconFile=..\assets\wheelconvert.ico
UninstallDisplayIcon={app}\{#AppExe}
LicenseFile=..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start WheelConvert when I sign in to Windows"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Registry]
; Same key and value name the app's own "Start with Windows" item uses; the value is the quoted exe path.
Root: HKCU; Subkey: "{#RunKeyPath}"; ValueType: string; ValueName: "{#RunValueName}"; ValueData: """{app}\{#AppExe}"""; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start WheelConvert now"; Flags: nowait postinstall skipifsilent

[Code]
const
  DotNetDownloadUrl = 'https://dotnet.microsoft.com/download/dotnet/8.0';

// The .NET installer records installed shared frameworks under HKLM; each value name is a version such as 8.0.11.
function IsDesktopRuntime8Installed: Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Copy(Names[I], 1, 2) = '8.' then
      begin
        Result := True;
        Exit;
      end;
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not IsDesktopRuntime8Installed then
  begin
    if SuppressibleMsgBox('WheelConvert needs the .NET 8 Desktop Runtime (x64), which was not found.' + #13#10 + #13#10 +
        'Open the download page now? Setup will close; run it again after installing the runtime.',
        mbConfirmation, MB_YESNO, IDYES) = IDYES then
      ShellExec('open', DotNetDownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    Result := False;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Current, Expected, DataDir: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    // Remove the Run value only when it points at this install, so another copy's value is left alone.
    Expected := '"' + ExpandConstant('{app}\{#AppExe}') + '"';
    if RegQueryStringValue(HKCU, '{#RunKeyPath}', '{#RunValueName}', Current) then
      if CompareText(Trim(Current), Expected) = 0 then
        RegDeleteValue(HKCU, '{#RunKeyPath}', '{#RunValueName}');
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    // Settings and log live here. Ask first; a silent uninstall never deletes them.
    DataDir := ExpandConstant('{localappdata}\{#AppName}');
    if (not UninstallSilent) and DirExists(DataDir) then
      if MsgBox('Also delete your WheelConvert settings and log?' + #13#10 + DataDir,
          mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
  end;
end;
