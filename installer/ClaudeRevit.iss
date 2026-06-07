; Inno Setup script for Claude Revit
;
; Build manually:
;   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\ClaudeRevit.iss
;
; CI passes the version via /DAppVersion=v1.2 — see .github/workflows/release.yml
;
; The installer:
;   - Targets per-user install (no admin)
;   - Installs to %AppData%\Autodesk\Revit\Addins\2027\
;   - Detects if Revit is running, asks to close
;   - Clean uninstall removes our files (but leaves user's history + API key)

#define MyAppName "Claude Revit"
#define MyAppPublisher "roubaudal-maker"
#define MyAppURL "https://github.com/roubaudal-maker/ClaudeRevit"
#define RevitVersion "2027"

#ifndef AppVersion
  #define AppVersion "v0.0-dev"
#endif

; Strip leading 'v' from tag for AppVersion field (Inno wants digits like 1.2.0)
#define VersionNumeric Copy(AppVersion, 2, 99)

[Setup]
AppId={{C8A3E9F4-7D2B-4E16-9A5C-3F8B6D4E2A1C}}
AppName={#MyAppName}
AppVersion={#VersionNumeric}
AppVerName={#MyAppName} {#AppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={userappdata}\Autodesk\Revit\Addins\{#RevitVersion}
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
OutputDir=output
OutputBaseFilename=ClaudeRevit-Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\ClaudeRevit.dll
CloseApplications=yes
CloseApplicationsFilter=*.dll
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\ClaudeRevit\bin\Release\release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Code]
var
  RevitWasRunning: Boolean;

function IsRevitRunning: Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  if Exec('powershell.exe',
          '-NoProfile -NonInteractive -Command "exit ([int](Get-Process -Name Revit -ErrorAction SilentlyContinue).Count -gt 0)"',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := (ResultCode = 1);
  end;
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  RevitWasRunning := IsRevitRunning;
  if RevitWasRunning then
  begin
    if MsgBox(
         'Autodesk Revit is currently running.' + #13#10 + #13#10 +
         'The installer can copy files now but they won''t take effect ' +
         'until you close and re-open Revit.' + #13#10 + #13#10 +
         'Continue anyway?',
         mbConfirmation, MB_YESNO) = IDNO then
      Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    if RevitWasRunning then
      MsgBox(
        'Installed. Close and re-open Revit to load the new version.' + #13#10 + #13#10 +
        'After Revit restarts, look for the Claude tab. ' +
        'If this is your first install, click the gear icon in the chat pane to set your Anthropic API key.',
        mbInformation, MB_OK);
  end;
end;

[Run]
Filename: "{#MyAppURL}/blob/main/README.md"; Description: "Open the README"; Flags: postinstall shellexec skipifsilent unchecked

[UninstallDelete]
Type: files; Name: "{app}\ClaudeRevit.dll"
Type: files; Name: "{app}\ClaudeRevit.addin"
Type: files; Name: "{app}\Anthropic.dll"
Type: files; Name: "{app}\Microsoft.Extensions.AI.Abstractions.dll"
Type: files; Name: "{app}\ClaudeRevit.pdb"
