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
AppUpdatesURL=https://github.com/michel-az-de/anamnesis/releases
DefaultDirName={localappdata}\Programs\Anamnesis
DefaultGroupName=Anamnesis
DisableProgramGroupPage=auto
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=Anamnesis-{#AppVersion}-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern dynamic
WizardImageFile={#SourceRoot}\tray\Anamnesis.png
WizardImageFileDynamicDark={#SourceRoot}\tray\Anamnesis.png
WizardImageBackColor=$10172E
WizardImageBackColorDynamicDark=$10172E
WizardSmallImageFile={#SourceRoot}\tray\Anamnesis.png
WizardSmallImageFileDynamicDark={#SourceRoot}\tray\Anamnesis.png
WizardSmallImageBackColor=$10172E
WizardSmallImageBackColorDynamicDark=$10172E
WizardKeepAspectRatio=yes
SetupIconFile={#SourceRoot}\tray\Anamnesis.ico
SetupLogging=yes
CloseApplications=no
RestartApplications=no
UsePreviousAppDir=yes
LicenseFile=Termos-de-Uso.txt
UninstallDisplayIcon={app}\tray\Anamnesis.ico
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
Name: "{group}\Anamnesis"; Filename: "{app}\tray\Anamnesis.Tray.exe"; WorkingDir: "{app}\tray"; IconFilename: "{app}\tray\Anamnesis.ico"
Name: "{autodesktop}\Anamnesis"; Filename: "{app}\tray\Anamnesis.Tray.exe"; WorkingDir: "{app}\tray"; IconFilename: "{app}\tray\Anamnesis.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Anamnesis"; ValueData: """{app}\tray\Anamnesis.Tray.exe"" --background"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\tray\Anamnesis.Tray.exe"; Description: "Iniciar Anamnesis"; Flags: nowait postinstall skipifsilent

[Code]
const
  ChaveDesinstalacao = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B762A4D8-3BA7-4FB4-9A0A-A8135AB0DF2E}_is1';
  ExecutavelTray = 'tray\Anamnesis.Tray.exe';
  ExecutavelWorker = 'worker\Anamnesis.Worker.exe';
  ArgumentoEncerrarParaAtualizacao = '--encerrar-para-atualizacao';

type
  TModoInstalacao = (miInstalar, miAtualizar, miReparar);

var
  ModoInstalacao: TModoInstalacao;
  DiretorioInstalacaoAnterior: String;
  VersaoInstalada: String;
  PaginaAcao: TOutputMsgWizardPage;

function PayloadObrigatorioExiste(const Diretorio: String): Boolean;
begin
  Result := (Diretorio <> '') and
    FileExists(AddBackslash(Diretorio) + ExecutavelTray) and
    FileExists(AddBackslash(Diretorio) + ExecutavelWorker);
end;

procedure DeterminarModoInstalacao;
var
  RegistroEncontrado: Boolean;
begin
  DiretorioInstalacaoAnterior := '';
  VersaoInstalada := '';
  RegQueryStringValue(
    HKCU,
    ChaveDesinstalacao,
    'InstallLocation',
    DiretorioInstalacaoAnterior);
  RegistroEncontrado := RegQueryStringValue(
    HKCU,
    ChaveDesinstalacao,
    'DisplayVersion',
    VersaoInstalada);

  if not RegistroEncontrado then
  begin
    ModoInstalacao := miInstalar;
  end
  else if not PayloadObrigatorioExiste(DiretorioInstalacaoAnterior) then
  begin
    ModoInstalacao := miReparar;
  end
  else if VersaoInstalada = '{#AppVersion}' then
  begin
    ModoInstalacao := miReparar;
  end
  else
  begin
    ModoInstalacao := miAtualizar;
  end;
end;

function NomeAcao: String;
begin
  case ModoInstalacao of
    miInstalar: Result := 'Instalar';
    miAtualizar: Result := 'Atualizar';
    miReparar: Result := 'Reparar';
  end;
end;

function DescricaoAcao: String;
begin
  case ModoInstalacao of
    miInstalar:
      Result :=
        'Nenhuma instalação anterior foi encontrada. O Anamnesis será instalado para este usuário.';
    miAtualizar:
      Result :=
        'Foi encontrada a versão ' + VersaoInstalada +
        '. O Anamnesis será atualizado para a versão {#AppVersion}.';
    miReparar:
      Result :=
        'Foi encontrada uma instalação desta versão ou um arquivo obrigatório ausente. ' +
        'Os binários do Anamnesis serão reparados sem apagar seus dados locais.';
  end;
end;

function DiretorioDoTrayInstalado: String;
begin
  Result := DiretorioInstalacaoAnterior;
  if Result = '' then
  begin
    Result := WizardDirValue;
  end;
end;

function TrayEstaEmExecucao: Boolean;
begin
  Result := CheckForMutexes('Local\Anamnesis.Tray.' + GetUserNameString);
end;

function WorkerEstaEmExecucao: Boolean;
var
  CodigoResultado: Integer;
  Saida: TExecOutput;
  Indice: Integer;
begin
  Result := False;
  try
    if ExecAndCaptureOutput(
      ExpandConstant('{sys}\tasklist.exe'),
      '/FI "IMAGENAME eq Anamnesis.Worker.exe" /FO CSV /NH',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      CodigoResultado,
      Saida) and (CodigoResultado = 0) then
    begin
      for Indice := 0 to GetArrayLength(Saida.StdOut) - 1 do
      begin
        if Pos('Anamnesis.Worker.exe', Saida.StdOut[Indice]) > 0 then
        begin
          Result := True;
          Exit;
        end;
      end;
    end;
  except
    Log('Não foi possível consultar o Worker em execução: ' + GetExceptionMessage);
  end;
end;

procedure SolicitarEncerramentoDoTray;
var
  CaminhoTray: String;
  CodigoResultado: Integer;
begin
  CaminhoTray := AddBackslash(DiretorioDoTrayInstalado) + ExecutavelTray;
  if not FileExists(CaminhoTray) then
  begin
    Log('Tray ativo sem executável no diretório conhecido: ' + CaminhoTray);
    Exit;
  end;

  if Exec(
    CaminhoTray,
    ArgumentoEncerrarParaAtualizacao,
    ExtractFileDir(CaminhoTray),
    SW_HIDE,
    ewWaitUntilTerminated,
    CodigoResultado) then
  begin
    Log('Solicitação de encerramento cooperativo enviada ao Tray. Código: ' + IntToStr(CodigoResultado));
  end
  else
  begin
    Log('Não foi possível solicitar o encerramento cooperativo do Tray: ' + SysErrorMessage(CodigoResultado));
  end;
end;

function AguardarEncerramentoDoTray: Boolean;
var
  Tentativa: Integer;
begin
  Result := not TrayEstaEmExecucao;
  if Result then
  begin
    Exit;
  end;

  SolicitarEncerramentoDoTray;
  for Tentativa := 1 to 20 do
  begin
    Sleep(500);
    if not TrayEstaEmExecucao then
    begin
      Result := True;
      Exit;
    end;
  end;

  Result := False;
end;

procedure InitializeWizard;
begin
  DeterminarModoInstalacao;
  PaginaAcao := CreateOutputMsgPage(
    wpLicense,
    'Preparar Anamnesis',
    'O instalador verificou esta instalação.',
    DescricaoAcao + #13#10 + #13#10 +
    'Configuração, banco, reuniões, gravações, transcrições e atas permanecem no seu computador.');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = wpSelectDir) and (ModoInstalacao <> miInstalar);
end;

function UpdateReadyMemo(
  Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := MemoDirInfo;
  if MemoGroupInfo <> '' then
  begin
    Result := Result + NewLine + MemoGroupInfo;
  end;
  if MemoTasksInfo <> '' then
  begin
    Result := Result + NewLine + MemoTasksInfo;
  end;
  Result := Result + NewLine + 'Ação: ' + NomeAcao + '.';
  Result := Result + NewLine +
    'Os dados locais do Anamnesis não serão removidos durante esta operação.';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if TrayEstaEmExecucao and not AguardarEncerramentoDoTray then
  begin
    Result :=
      'O Anamnesis continua aberto. Feche-o pela bandeja do Windows e execute o instalador novamente. ' +
      'Se houver uma gravação ativa, finalize-a antes de atualizar ou reparar.';
    Exit;
  end;

  if WorkerEstaEmExecucao then
  begin
    Result :=
      'O Anamnesis ainda está processando uma reunião. Aguarde o Worker terminar e execute o instalador novamente. ' +
      'Nenhum processo foi encerrado à força.';
  end;
end;
