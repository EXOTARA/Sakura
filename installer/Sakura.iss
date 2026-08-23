#ifndef MyAppVersion
  #define MyAppVersion "0.29.0-beta"
#endif

#ifndef MyNumericVersion
  #define MyNumericVersion "0.27.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

#define MyAppName "Sakura"
#define MyAppPublisher "EXOTARA"
#define MyAppExeName "Sakura.exe"
#define MyAppId "{{5F9D061E-33C8-4F85-BE6E-8C3BAF240B85}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/EXOTARA/Sakura
AppSupportURL=https://github.com/EXOTARA/Sakura/issues
DefaultDirName={localappdata}\Programs\Sakura
DefaultGroupName=Sakura
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=Sakura-{#MyAppVersion}-Setup
SetupIconFile=..\src\Nexo.App\Assets\Sakura.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no
AllowNoIcons=yes
MinVersion=10.0.19041
VersionInfoVersion={#MyNumericVersion}
VersionInfoCompany=EXOTARA
VersionInfoDescription=Instalador de Sakura
VersionInfoProductName=Sakura
VersionInfoProductVersion={#MyNumericVersion}

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

; Diseño D70 — al actualizar sobre una instalación con el nombre anterior, el ejecutable viejo se
; queda huérfano en la carpeta y el acceso directo sigue apuntándolo. Se borra explícitamente: el
; registro de desinstalación de Inno solo conoce lo que él mismo puso, y Kohana.exe lo puso otra
; versión con otro nombre.
[InstallDelete]
Type: files; Name: "{app}\Kohana.exe"
Type: files; Name: "{group}\Kohana.lnk"
Type: files; Name: "{autodesktop}\Kohana.lnk"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked
Name: "startup"; Description: "Iniciar Sakura con Windows"; GroupDescription: "Integración con Windows:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Sakura"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Sakura"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Sakura"; ValueData: """{app}\{#MyAppExeName}"" --background"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir Sakura"; Flags: nowait postinstall skipifsilent

[Code]
const
  RunKey = 'Software\Microsoft\Windows\CurrentVersion\Run';

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDirectory: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    RegDeleteValue(HKCU, RunKey, 'Sakura');
    RegDeleteValue(HKCU, RunKey, 'Kohana');
    RegDeleteValue(HKCU, RunKey, 'Nexo');
  end;

  if CurUninstallStep = usPostUninstall then
  begin
    DataDirectory := ExpandConstant('{localappdata}\Sakura');
    if DirExists(DataDirectory) and
       (MsgBox(
          '¿También quieres eliminar las tareas, rutinas, preferencias e historial local de Sakura?',
          mbConfirmation,
          MB_YESNO) = IDYES) then
    begin
      DelTree(DataDirectory, True, True, True);
    end;
  end;
end;
