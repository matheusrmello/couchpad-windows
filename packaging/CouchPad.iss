#ifndef Payload
  #error Specify the absolute payload directory
#endif
#ifndef Output
  #error Specify the absolute output directory
#endif
#ifndef Version
  #error Specify the product version
#endif

[Setup]
AppId=CouchPad.Windows
AppName=CouchPad
AppVersion={#Version}
AppPublisher=CouchPad
DefaultDirName={localappdata}\Programs\CouchPad
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.18362
OutputDir={#Output}
OutputBaseFilename=CouchPad-Setup-{#Version}-win-x64
SetupIconFile=..\src\CouchPad.App\Assets\couchpad.ico
UninstallDisplayIcon={app}\CouchPad.exe
CloseApplications=no
RestartApplications=no
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
en.CloseCouchPad=Exit CouchPad from its notification area menu and complete any pending recovery before continuing.
ptbr.CloseCouchPad=Saia do CouchPad pelo menu da bandeja e conclua qualquer recuperação pendente antes de continuar.
en.TaskFailed=Windows could not configure the CouchPad recovery task. Check the local CouchPad rescue log.
ptbr.TaskFailed=O Windows não conseguiu configurar a tarefa de recuperação do CouchPad. Consulte o log local do rescue.
en.TaskConflict=Another CouchPad installation owns the startup or recovery registration for this account. Remove that installation's registration before continuing.
ptbr.TaskConflict=Outra instalação do CouchPad é responsável pela inicialização ou recuperação nesta conta. Remova o registro dessa instalação antes de continuar.

[Files]
Source: "{#Preflight}"; Flags: dontcopy
Source: "{#Payload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\CouchPad"; Filename: "{app}\CouchPad.exe"; WorkingDir: "{app}"

[Code]
function RunRescue(const Argument: String): Boolean;
var
  ExitCode: Integer;
begin
  Result := Exec(ExpandConstant('{app}\CouchPad.Rescue.exe'), Argument,
    ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ExitCode);
  if Result then
    Result := ExitCode = 0;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  ExtractTemporaryFile('CouchPad.InstallCheck.exe');
  if not Exec(ExpandConstant('{tmp}\CouchPad.InstallCheck.exe'),
    '--check-install-target "' + ExpandConstant('{app}') + '"',
    ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    Result := CustomMessage('TaskFailed')
  else if ExitCode = 3 then
    Result := CustomMessage('TaskConflict')
  else if ExitCode <> 0 then
    Result := CustomMessage('CloseCouchPad');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and not RunRescue('--install-logon-task') then
    RaiseException(CustomMessage('TaskFailed'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usUninstall) and not RunRescue('--remove-logon-task') then
    RaiseException(CustomMessage('CloseCouchPad') + #13#10 + CustomMessage('TaskFailed'));
end;
