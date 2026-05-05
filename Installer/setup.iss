; ============================================================
;  Finanzas Personales — Script de instalador (Inno Setup 6)
;  Genera:  Output\FinanzasPersonales-Setup.exe
;
;  Requisito: Inno Setup 6  https://jrsoftware.org/isinfo.php
;  Antes de compilar este script ejecuta:
;    dotnet publish ..\FinanzasPersonales.csproj -p:PublishProfile=win-x64-installer
; ============================================================

#define AppName      "Finanzas Personales"
#define AppVersion   "1.0.0"
#define AppExe       "FinanzasPersonales.exe"
#define PublishDir   "..\publish"
#define OutputDir    "..\Installer\Output"

[Setup]
AppId={{A3F7C2D1-8E4B-4F19-9A2C-5D6E7F8A9B0C}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=FinanzasPersonales
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir={#OutputDir}
OutputBaseFilename=FinanzasPersonales-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardResizable=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UninstallDisplayName={#AppName}
CloseApplications=yes
; Si tienes un icono .ico descomenta la siguiente línea:
; SetupIconFile=..\Resources\app.ico

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon";   Description: "Crear acceso directo en el &escritorio";    GroupDescription: "Íconos adicionales:"
Name: "startupicon";   Description: "Iniciar con &Windows (al arrancar el equipo)"; GroupDescription: "Íconos adicionales:"; Flags: unchecked

[Files]
; Ejecutable principal (autónomo, incluye el runtime de .NET)
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
; Fuentes requeridas por QuestPDF para la generación de reportes PDF
Source: "{#PublishDir}\LatoFont\*"; DestDir: "{app}\LatoFont"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: DirExists(ExpandConstant('{#PublishDir}\LatoFont'))

[Icons]
Name: "{group}\{#AppName}";                  Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar {#AppName}";      Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";          Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}";            Filename: "{app}\{#AppExe}"; Tasks: startupicon

[Registry]
; Registra la aplicación en "Programas predeterminados" (solo informativo)
Root: HKCU; Subkey: "Software\FinanzasPersonales"; ValueType: string; \
  ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#AppExe}"; \
  Description: "Iniciar {#AppName} ahora"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
; No elimina la base de datos del usuario al desinstalar (en %APPDATA%)
Type: filesandordirs; Name: "{app}"
