; WheelConvert installer (WP-12). Per-user, no administrator rights.
; Build:  iscc /DAppVersion=0.1.0 /DSourceDir=<folder with WheelConvert.exe, its native DLLs, LICENSE, THIRD-PARTY-NOTICES.md and licenses\> /DOutputDir=<folder> installer\WheelConvert.iss
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
; Must match MutexName in src/WheelConvert.App/App.xaml.cs (the app creates "Local\WheelConvert.SingleInstance"; Setup
; opens the bare name, which resolves to the same per-session object). Setup and the uninstaller then ask the user to close a running app.
AppMutex=WheelConvert.SingleInstance

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start WheelConvert when I sign in to Windows"; Flags: unchecked

[Files]
; Everything the publish step produced next to the exe: WheelConvert.exe, the native DLLs (Magick.Native, libSkiaSharp, pdfium)
; and LICENSE / THIRD-PARTY-NOTICES.md. The native DLLs stay separate files so a user can replace an LGPL library (THIRD-PARTY-NOTICES.md).
; No skipifsourcedoesntexist: a build without the notice files must fail.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "licenses,*.pdb"; Flags: ignoreversion
Source: "{#SourceDir}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs

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

// Is the x64 .NET 8 Desktop Runtime installed? Checked in this order:
// 1. HKLM, 32-bit registry view. The .NET installer writes InstalledVersions\<arch>\sharedfx\<framework> there, one value per
//    version (8.0.34 ...). "Must be accessed through the 32-bit registry view":
//    https://github.com/dotnet/designs/blob/main/accepted/2020/install-locations.md
//    <arch> is x64, so on an ARM64 PC this finds the x64 runtime the x64 app needs, not the arm64 one. Seen on a real PC with reg query.
// 2. HKLM, 64-bit view (in case a future installer writes it there).
// 3. Folder fallback: <Program Files>\dotnet\shared\Microsoft.WindowsDesktop.App\8.*
//    https://learn.microsoft.com/dotnet/core/install/how-to-detect-installed-versions
//    On ARM64 that folder holds the arm64 runtime; Microsoft says the x64 SDK goes to <Program Files>\dotnet\x64\, so there we look in
//    dotnet\x64\shared instead (https://learn.microsoft.com/dotnet/core/install/windows, "Arm-based Windows PCs").
//    That ARM64 path is documented for the SDK only; the runtime path is assumed to match and was not tested on ARM64 hardware.
function HasDesktopRuntime8InKey(RootKey: Integer): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(RootKey, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Copy(Names[I], 1, 2) = '8.' then
      begin
        Result := True;
        Exit;
      end;
end;

function HasDesktopRuntime8InFolder: Boolean;
var
  Root: String;
  FindRec: TFindRec;
begin
  Result := False;
  Root := ExpandConstant('{commonpf64}\dotnet');
  if ProcessorArchitecture = paARM64 then
    Root := Root + '\x64';
  if FindFirst(Root + '\shared\Microsoft.WindowsDesktop.App\8.*', FindRec) then
  try
    Result := True;
  finally
    FindClose(FindRec);
  end;
end;

function IsDesktopRuntime8Installed: Boolean;
begin
  Result := HasDesktopRuntime8InKey(HKLM32) or HasDesktopRuntime8InKey(HKLM64) or HasDesktopRuntime8InFolder;
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not IsDesktopRuntime8Installed then
  begin
    // Default answer is No: with /SUPPRESSMSGBOXES (silent installs, winget) no browser is ever opened.
    if SuppressibleMsgBox('WheelConvert needs the .NET 8 Desktop Runtime (x64), which was not found.' + #13#10 + #13#10 +
        'Open the download page now? Setup will close; run it again after installing the runtime.',
        mbConfirmation, MB_YESNO, IDNO) = IDYES then
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
