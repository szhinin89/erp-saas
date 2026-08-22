; ZH Print Agent - Windows installer (Inno Setup)
;
; Builds a double-click .exe that installs ZH.PrintAgent.App as a Windows Service, provisions the
; persistent ProgramData folders, and leaves the till ready to open the local /admin setup wizard.
;
; Build with: print-agent\scripts\build-installer.ps1 (checks for ISCC.exe and runs the publish step
; first if needed). Requires print-agent\publish\win-x64 to already contain a self-contained win-x64
; publish of ZH.PrintAgent.App (see publish-win-x64.ps1).
;
; MSI/WiX packaging is intentionally out of scope for now; this script is deliberately self-contained
; so it can be swapped for (or complemented by) a WiX project later without touching the publish step.

#define MyAppName "ZH Print Agent"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "ZH Technologies"
#define MyServiceName "ZHPrintAgent"
#define MyServiceDisplayName "ZH Print Agent"
#define MyAdminUrl "http://127.0.0.1:9817/admin"
#define MyPublishDir "..\..\..\publish\win-x64"
#define MyDataRoot "C:\ProgramData\ZH Technologies\PrintAgent"

[Setup]
AppId={{A6B9F2E1-4C3D-4B7E-9F1A-8D2C6E5B7A31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ZH Technologies\PrintAgent
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\..\..\publish\installer
OutputBaseFilename=ZH-Print-Agent-Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\ZH.PrintAgent.App.exe
WizardStyle=modern

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Safe, secret-free defaults (loopback bind, sentinel API key, SetupCompleted=false, no printers) - the
; local /admin wizard generates the real API key and printer config on first run. Never overwritten on
; update/reinstall so a manually customized production config on this till is preserved.
Source: "..\appsettings.Production.sample.json"; DestDir: "{app}"; DestName: "appsettings.Production.json"; Check: AppSettingsProductionMissing

[Dirs]
; Created once and never removed by the uninstaller - this is where the wizard's generated API key,
; printer config, print queue, and logs live. Preserved by default across uninstall/update.
Name: "{#MyDataRoot}\config"; Flags: uninsneveruninstall
Name: "{#MyDataRoot}\data"; Flags: uninsneveruninstall
Name: "{#MyDataRoot}\logs"; Flags: uninsneveruninstall
Name: "{#MyDataRoot}\queue"; Flags: uninsneveruninstall
Name: "{#MyDataRoot}\printed"; Flags: uninsneveruninstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{#MyAdminUrl}"
Name: "{group}\{#MyAppName} Configuración"; Filename: "{#MyAdminUrl}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{#MyAdminUrl}"; Description: "Abrir el panel de configuración de ZH Print Agent"; Flags: postinstall shellexec skipifsilent

[Code]
function AppSettingsProductionMissing: Boolean;
begin
  // Never clobber a real, already-configured appsettings.Production.json on update/reinstall.
  Result := not FileExists(ExpandConstant('{app}\appsettings.Production.json'));
end;

function ServiceExists(const ServiceName: String): Boolean;
var
  ResultCode: Integer;
begin
  Exec('sc.exe', 'query ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := (ResultCode = 0);
end;

procedure StopServiceIfRunning(const ServiceName: String);
var
  ResultCode: Integer;
begin
  if ServiceExists(ServiceName) then
  begin
    Exec('sc.exe', 'stop ' + ServiceName, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Sleep(2000);
  end;
end;

procedure CreateServiceIfMissing;
var
  ResultCode: Integer;
  ExePath, CreateParams: String;
begin
  if ServiceExists('{#MyServiceName}') then
    Exit;

  ExePath := ExpandConstant('{app}\ZH.PrintAgent.App.exe');
  CreateParams := 'create {#MyServiceName} binPath= "' + ExePath + '" start= auto ' +
    'DisplayName= "{#MyServiceDisplayName}"';
  Exec('sc.exe', CreateParams, '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('sc.exe', 'description {#MyServiceName} "Local ZH Technologies POS receipt print agent."',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure ConfigureServiceRecovery;
var
  ResultCode: Integer;
begin
  // At least 3 restart attempts with an increasing delay, matching scripts\install-windows-service.ps1.
  Exec('sc.exe', 'failure {#MyServiceName} reset= 60 actions= restart/5000/restart/10000/restart/30000',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('sc.exe', 'failureflag {#MyServiceName} 1', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure StartService;
var
  ResultCode: Integer;
begin
  Exec('sc.exe', 'start {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure DeleteService;
var
  ResultCode: Integer;
begin
  if ServiceExists('{#MyServiceName}') then
    Exec('sc.exe', 'delete {#MyServiceName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
  begin
    // Stop before copying files so an update never fails to overwrite a locked, running .exe.
    StopServiceIfRunning('{#MyServiceName}');
  end;

  if CurStep = ssPostInstall then
  begin
    CreateServiceIfMissing;
    ConfigureServiceRecovery;
    StartService;
    MsgBox('Si el navegador no se abrió automáticamente, visita ' + '{#MyAdminUrl}' +
      ' para completar la configuración inicial: generar la API key, elegir la impresora, ' +
      'el driver windows-raw, el ancho de papel y hacer una impresión de prueba.',
      mbInformation, MB_OK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    StopServiceIfRunning('{#MyServiceName}');
    DeleteService;
  end;
end;
