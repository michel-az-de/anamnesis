#ifndef AppVersion
  #define AppVersion "0.2.0-beta.1"
#endif
#ifndef AppNumericVersion
  #define AppNumericVersion "0.2.0.0"
#endif
#ifndef SourceRoot
  #error SourceRoot deve ser informado ao compilador.
#endif
#ifndef OutputDir
  #error OutputDir deve ser informado ao compilador.
#endif

[Setup]
AppId={{B762A4D8-3BA7-4FB4-9A0A-A8135AB0DF2E}
AppName=Anamnesis
AppVersion={#AppVersion}
AppPublisher=Anamnesis contributors
AppPublisherURL=https://github.com/michel-az-de/anamnesis
AppSupportURL=https://github.com/michel-az-de/anamnesis/issues
DefaultDirName={localappdata}\Programs\Anamnesis
DefaultGroupName=Anamnesis
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=Anamnesis-{#AppVersion}-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile={#SourceRoot}\tray\Anamnesis.ico
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\tray\Anamnesis.Tray.exe
VersionInfoVersion={#AppNumericVersion}
VersionInfoDescription=Instalador do Anamnesis
VersionInfoProductName=Anamnesis
VersionInfoProductVersion={#AppNumericVersion}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na area de trabalho"; GroupDescription: "Atalhos adicionais:"; Flags: unchecked
Name: "startup"; Description: "Iniciar Anamnesis com o Windows"; GroupDescription: "Inicializacao:"; Flags: checkedonce

[Files]
Source: "{#SourceRoot}\tray\*"; DestDir: "{app}\tray"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\worker\*"; DestDir: "{app}\worker"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Anamnesis"; Filename: "{app}\tray\Anamnesis.Tray.exe"; WorkingDir: "{app}\tray"
Name: "{autodesktop}\Anamnesis"; Filename: "{app}\tray\Anamnesis.Tray.exe"; WorkingDir: "{app}\tray"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Anamnesis"; ValueData: """{app}\tray\Anamnesis.Tray.exe"" --background"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\tray\Anamnesis.Tray.exe"; Description: "Iniciar Anamnesis"; Flags: nowait postinstall skipifsilent
