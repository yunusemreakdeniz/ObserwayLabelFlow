; Obserway LabelFlow — Inno Setup installer
; Derleme: scripts\publish-installer.ps1

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\publish\ObserwayLabelFlow"
#endif

#define MyAppName "Obserway LabelFlow"
#define MyAppPublisher "Obserway"
#define MyAppURL "https://obserway.com"
#define MyAppExeName "ObserwayLabelFlow.exe"
#define MyAppId "{{A8F4C2E1-9B3D-4F6A-8C7E-1D2E3F4A5B6C}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\Obserway LabelFlow
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=
OutputDir=..\dist
OutputBaseFilename=ObserwayLabelFlow-Setup-{#MyAppVersion}
SetupIconFile=..\ObserwayLabelFlow.App\Assets\obserway.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoDescription={#MyAppName} Setup

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "redist\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not IsWebView2Installed

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName} Kaldır"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "WebView2 Runtime kuruluyor..."; Check: not IsWebView2Installed; Flags: waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsWebView2Installed: Boolean;
var
  Version: String;
begin
  Result :=
    RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) or
    RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version);
end;

function InitializeSetup: Boolean;
begin
  if not FileExists(ExpandConstant('{#PublishDir}\{#MyAppExeName}')) then
  begin
    MsgBox('Yayın klasörü bulunamadı.' + #13#10 +
           'Önce scripts\publish-installer.ps1 çalıştırın.', mbError, MB_OK);
    Result := False;
  end
  else if (not IsWebView2Installed) and (not FileExists(ExpandConstant('{src}\redist\MicrosoftEdgeWebview2Setup.exe'))) then
  begin
    MsgBox('WebView2 bootstrapper eksik: installer\redist\MicrosoftEdgeWebview2Setup.exe' + #13#10 +
           'publish-installer.ps1 script''i bunu otomatik indirir.', mbError, MB_OK);
    Result := False;
  end
  else
    Result := True;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[UninstallRun]
; Kullanıcı verileri (LocalAppData) bilinçli olarak silinmez.
