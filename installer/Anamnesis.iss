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
PrivilegesRequired=admin
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

[InstallDelete]
Type: filesandordirs; Name: "{app}\tray"
Type: filesandordirs; Name: "{app}\worker"

[Files]
Source: "{#SourceRoot}\tray\*"; DestDir: "{app}\tray"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\worker\*"; DestDir: "{app}\worker"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\LICENSE"; DestDir: "{app}"; DestName: "LICENSE"; Flags: ignoreversion

[Icons]
Name: "{group}\Anamnesis"; Filename: "{app}\tray\Anamnesis.Tray.exe"; WorkingDir: "{app}\tray"; IconFilename: "{app}\tray\Anamnesis.ico"
Name: "{autodesktop}\Anamnesis"; Filename: "{app}\tray\Anamnesis.Tray.exe"; WorkingDir: "{app}\tray"; IconFilename: "{app}\tray\Anamnesis.ico"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Anamnesis"; ValueData: """{app}\tray\Anamnesis.Tray.exe"" --background"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\tray\Anamnesis.Tray.exe"; Description: "Iniciar Anamnesis"; Flags: nowait postinstall skipifsilent; Check: DeveAbrirAposInstalacaoNova

[Code]
const
  ChaveDesinstalacao = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B762A4D8-3BA7-4FB4-9A0A-A8135AB0DF2E}_is1';
  ChaveDiagnostico = 'Software\Anamnesis\Instalador';
  NomeValorUltimoDiagnostico = 'UltimoDiagnostico';
  ExecutavelTray = 'tray\Anamnesis.Tray.exe';
  ExecutavelWorker = 'worker\Anamnesis.Worker.exe';
  ArgumentoEncerrarParaAtualizacao = '--encerrar-para-atualizacao';

type
  TModoInstalacao = (miInstalar, miAtualizar, miReparar);
  TEstadoWorker = (ewAusente, ewExecutando, ewDesconhecido);

var
  ModoInstalacao: TModoInstalacao;
  DiretorioInstalacaoAnterior: String;
  VersaoInstalada: String;
  PaginaDiagnostico: TOutputMsgWizardPage;
  PaginaAcao: TInputOptionWizardPage;
  DowngradeDetectado: Boolean;
  InstalacaoAnteriorDetectada: Boolean;
  WorkerComVersaoDivergente: Boolean;
  UltimoDiagnosticoAnterior: String;

function PayloadObrigatorioExiste(const Diretorio: String): Boolean;
begin
  Result := (Diretorio <> '') and
    FileExists(AddBackslash(Diretorio) + ExecutavelTray) and
    FileExists(AddBackslash(Diretorio) + ExecutavelWorker);
end;

function DiretorioPadraoInstalacao: String;
begin
  Result := ExpandConstant('{localappdata}\Programs\Anamnesis');
end;

function CaminhoLogDiagnostico: String;
begin
  Result := ExpandConstant('{commonappdata}\Anamnesis\Logs\instalador.log');
end;

procedure RegistrarDiagnostico(const Categoria, Mensagem: String);
var
  Linha: String;
  Arquivo: String;
begin
  Linha := GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':') + ' [' + Categoria + '] ' + Mensagem;
  Log(Linha);
  Arquivo := CaminhoLogDiagnostico;
  if ForceDirectories(ExtractFileDir(Arquivo)) then
  begin
    SaveStringToFile(Arquivo, Linha + #13#10, True);
  end;
  RegWriteStringValue(HKLM, ChaveDiagnostico, NomeValorUltimoDiagnostico, Linha);
  RegWriteStringValue(HKCU, ChaveDiagnostico, NomeValorUltimoDiagnostico, Linha);
end;

function ObterUltimoDiagnostico: String;
begin
  Result := '';
  if not RegQueryStringValue(HKLM, ChaveDiagnostico, NomeValorUltimoDiagnostico, Result) then
  begin
    if not RegQueryStringValue(HKCU, ChaveDiagnostico, NomeValorUltimoDiagnostico, Result) then
    begin
      Result := 'Nenhum diagnóstico anterior foi registrado.';
    end;
  end;
end;

function CompararVersaoDoExecutavel(
  const Caminho: String;
  const VersaoBinariaNova: Int64;
  var ComparacaoAcumulada: Integer): Boolean;
var
  VersaoBinariaInstalada: Int64;
  ComparacaoAtual: Integer;
begin
  Result := GetPackedVersion(Caminho, VersaoBinariaInstalada);
  if not Result then
  begin
    Exit;
  end;

  ComparacaoAtual := ComparePackedVersion(VersaoBinariaInstalada, VersaoBinariaNova);
  if ComparacaoAtual > 0 then
  begin
    ComparacaoAcumulada := ComparacaoAtual;
  end
  else if (ComparacaoAtual < 0) and (ComparacaoAcumulada = 0) then
  begin
    ComparacaoAcumulada := ComparacaoAtual;
  end;
end;

function WorkerPossuiVersaoDivergente(const Diretorio: String): Boolean;
var
  VersaoBinariaTray: Int64;
  VersaoBinariaWorker: Int64;
begin
  Result := False;
  if not GetPackedVersion(
    AddBackslash(Diretorio) + ExecutavelTray,
    VersaoBinariaTray) then
  begin
    Exit;
  end;

  if not GetPackedVersion(
    AddBackslash(Diretorio) + ExecutavelWorker,
    VersaoBinariaWorker) then
  begin
    Exit;
  end;

  Result := ComparePackedVersion(VersaoBinariaWorker, VersaoBinariaTray) <> 0;
end;

function CompararVersoesDisponiveis(
  const Diretorio: String;
  var ComparacaoAcumulada: Integer): Boolean;
var
  VersaoBinariaNova: Int64;
begin
  Result := False;
  ComparacaoAcumulada := 0;
  if not GetPackedVersion(ExpandConstant('{srcexe}'), VersaoBinariaNova) then
  begin
    Exit;
  end;

  Result := CompararVersaoDoExecutavel(
    AddBackslash(Diretorio) + ExecutavelTray,
    VersaoBinariaNova,
    ComparacaoAcumulada);
end;

procedure DeterminarModoInstalacao;
var
  RegistroEncontrado: Boolean;
  VersaoEncontrada: Boolean;
  ComparacaoDeVersao: Integer;
  ComparacaoDeVersaoDisponivel: Boolean;
begin
  DiretorioInstalacaoAnterior := DiretorioPadraoInstalacao;
  VersaoInstalada := '';
  DowngradeDetectado := False;
  InstalacaoAnteriorDetectada := False;
  WorkerComVersaoDivergente := False;
  if not RegQueryStringValue(
    HKLM,
    ChaveDesinstalacao,
    'InstallLocation',
    DiretorioInstalacaoAnterior) then
  begin
    RegQueryStringValue(
      HKCU,
      ChaveDesinstalacao,
      'InstallLocation',
      DiretorioInstalacaoAnterior);
  end;
  RegistroEncontrado := RegKeyExists(HKLM, ChaveDesinstalacao) or
    RegKeyExists(HKCU, ChaveDesinstalacao);
  InstalacaoAnteriorDetectada := RegistroEncontrado or DirExists(DiretorioInstalacaoAnterior);
  VersaoEncontrada := RegQueryStringValue(
    HKLM,
    ChaveDesinstalacao,
    'DisplayVersion',
    VersaoInstalada);
  if not VersaoEncontrada then
  begin
    VersaoEncontrada := RegQueryStringValue(
      HKCU,
      ChaveDesinstalacao,
      'DisplayVersion',
      VersaoInstalada);
  end;

  ComparacaoDeVersaoDisponivel := False;
  if InstalacaoAnteriorDetectada then
  begin
    ComparacaoDeVersaoDisponivel :=
      CompararVersoesDisponiveis(DiretorioInstalacaoAnterior, ComparacaoDeVersao);
    WorkerComVersaoDivergente :=
      WorkerPossuiVersaoDivergente(DiretorioInstalacaoAnterior);
  end;

  if not InstalacaoAnteriorDetectada then
  begin
    ModoInstalacao := miInstalar;
  end
  else if ComparacaoDeVersaoDisponivel and (ComparacaoDeVersao > 0) then
  begin
    DowngradeDetectado := True;
    ModoInstalacao := miReparar;
  end
  else if not PayloadObrigatorioExiste(DiretorioInstalacaoAnterior) then
  begin
    ModoInstalacao := miReparar;
  end
  else if ComparacaoDeVersaoDisponivel and (ComparacaoDeVersao < 0) then
  begin
    ModoInstalacao := miAtualizar;
  end
  else if WorkerComVersaoDivergente then
  begin
    ModoInstalacao := miReparar;
  end
  else if ComparacaoDeVersaoDisponivel or not VersaoEncontrada or
    (VersaoInstalada = '{#AppVersion}') then
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
  if DowngradeDetectado then
  begin
    Result := 'Manter versão mais recente';
    Exit;
  end;

  case ModoInstalacao of
    miInstalar: Result := 'Instalar';
    miAtualizar: Result := 'Atualizar';
    miReparar: Result := 'Reparar';
  end;
end;

function DescricaoAcao: String;
begin
  if DowngradeDetectado then
  begin
    Result :=
      'A versão instalada é mais recente que este pacote. O downgrade foi bloqueado para proteger os dados locais.';
    Exit;
  end;

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
        'Foi encontrada uma instalação desta versão, um componente com versão divergente ou um arquivo obrigatório ausente. ' +
        'Os binários do Anamnesis serão reparados sem apagar seus dados locais.';
  end;
end;

function ResumoDiagnostico: String;
var
  Integridade: String;
begin
  if not PayloadObrigatorioExiste(DiretorioInstalacaoAnterior) and InstalacaoAnteriorDetectada then
  begin
    Integridade := 'Incompleta: falta ao menos um executável obrigatório. O reparo é recomendado.';
  end
  else if WorkerComVersaoDivergente then
  begin
    Integridade := 'Inconsistente: a versão do Worker diverge do Tray. O reparo é recomendado.';
  end
  else if PayloadObrigatorioExiste(DiretorioInstalacaoAnterior) then
  begin
    Integridade := 'Íntegra: Tray e Worker foram encontrados.';
  end
  else
  begin
    Integridade := 'Não existe instalação anterior neste usuário.';
  end;

  Result :=
    'Instalação: ' + DiretorioInstalacaoAnterior + #13#10 +
    'Versão instalada: ' + VersaoInstalada + #13#10 +
    'Versão deste pacote: {#AppVersion}' + #13#10 +
    'Integridade: ' + Integridade + #13#10 + #13#10 +
    'Último diagnóstico conhecido:' + #13#10 +
    UltimoDiagnosticoAnterior;
end;

function TextoDaOpcaoRecomendada: String;
begin
  if DowngradeDetectado then
  begin
    Result := 'Manter a versão mais recente já instalada. Este pacote não fará downgrade.';
    Exit;
  end;

  case ModoInstalacao of
    miInstalar:
      Result := 'Instalar o Anamnesis neste computador.';
    miAtualizar:
      Result := 'Atualizar os binários e preservar todos os dados locais.';
    miReparar:
      Result := 'Reparar os binários desta instalação sem apagar dados locais.';
  end;
end;

function DiretorioDoTrayInstalado: String;
begin
  Result := DiretorioInstalacaoAnterior;
  if Result = '' then
  begin
    Result := ExpandConstant('{app}');
  end;
end;

function TrayEstaEmExecucao: Boolean;
begin
  Result := CheckForMutexes('Local\Anamnesis.Tray.' + GetUserNameString);
end;

function ConsultarEstadoWorker: TEstadoWorker;
var
  CodigoResultado: Integer;
  Saida: TExecOutput;
  Indice: Integer;
begin
  Result := ewDesconhecido;
  try
    if ExecAndCaptureOutput(
      ExpandConstant('{sys}\tasklist.exe'),
      '/FI "IMAGENAME eq Anamnesis.Worker.exe" /FO CSV /NH',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      CodigoResultado,
      Saida) then
    begin
      if CodigoResultado <> 0 then
      begin
        Log('tasklist devolveu código ' + IntToStr(CodigoResultado) + '.');
        Exit;
      end;

      Result := ewAusente;
      for Indice := 0 to GetArrayLength(Saida.StdOut) - 1 do
      begin
        if Pos('Anamnesis.Worker.exe', Saida.StdOut[Indice]) > 0 then
        begin
          Result := ewExecutando;
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
  if InstalacaoAnteriorDetectada and (DiretorioInstalacaoAnterior <> '') then
  begin
    WizardForm.DirEdit.Text := DiretorioInstalacaoAnterior;
  end;
  UltimoDiagnosticoAnterior := ObterUltimoDiagnostico;
  PaginaDiagnostico := CreateOutputMsgPage(
    wpLicense,
    'Diagnóstico da instalação',
    'O Anamnesis verificou sua instalação antes de alterar qualquer arquivo.',
    ResumoDiagnostico + #13#10 + #13#10 +
    'Configuração, banco, reuniões, gravações, transcrições e atas permanecem no seu computador.');
  PaginaAcao := CreateInputOptionPage(
    PaginaDiagnostico.ID,
    'Ação recomendada',
    NomeAcao,
    DescricaoAcao + #13#10 + #13#10 +
    'Confirme a opção abaixo para continuar.',
    True,
    False);
  PaginaAcao.Add(TextoDaOpcaoRecomendada);
  PaginaAcao.SelectedValueIndex := 0;
  RegistrarDiagnostico('INFO', 'Wizard iniciado. Ação recomendada: ' + NomeAcao + '.');
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

function ValidarProntidaoParaInstalar(var Mensagem: String): Boolean;
var
  EstadoWorker: TEstadoWorker;
begin
  Result := False;
  Mensagem := '';
  if DowngradeDetectado then
  begin
    Mensagem :=
      'Este instalador é mais antigo que a versão já instalada. Baixe uma versão igual ou mais recente; ' +
      'o Anamnesis não executa downgrade automático.';
    RegistrarDiagnostico('AVISO', Mensagem);
    Exit;
  end;

  if TrayEstaEmExecucao and not AguardarEncerramentoDoTray then
  begin
    Mensagem :=
      'O Anamnesis continua aberto. Finalize uma gravação ativa ou feche-o pela bandeja do Windows. ' +
      'O wizard continua aberto para você tentar novamente, sem cancelar a instalação.';
    RegistrarDiagnostico('AVISO', Mensagem);
    Exit;
  end;

  EstadoWorker := ConsultarEstadoWorker;
  if EstadoWorker = ewExecutando then
  begin
    Mensagem :=
      'O Anamnesis ainda está processando uma reunião. Aguarde o Worker terminar e execute o instalador novamente. ' +
      'Nenhum processo foi encerrado à força.';
    RegistrarDiagnostico('AVISO', Mensagem);
    Exit;
  end;

  if EstadoWorker = ewDesconhecido then
  begin
    Mensagem :=
      'O instalador não conseguiu confirmar se o Worker está em execução. ' +
      'Por segurança, nenhuma alteração foi feita. Tente novamente ou reinicie o Windows.';
    RegistrarDiagnostico('ERRO', Mensagem);
    Exit;
  end;

  Result := True;
  RegistrarDiagnostico('INFO', 'Preflight concluído. Instalação pode continuar.');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Mensagem: String;
begin
  Result := True;
  if (CurPageID = PaginaAcao.ID) and DowngradeDetectado then
  begin
    SuppressibleMsgBox(
      'A versão instalada é mais recente. Nenhuma alteração será feita. ' +
      'Use um instalador igual ou mais recente para atualizar.',
      mbInformation,
      MB_OK,
      IDOK);
    Result := False;
    Exit;
  end;

  if (CurPageID = wpReady) and not ValidarProntidaoParaInstalar(Mensagem) then
  begin
    WizardForm.NextButton.Caption := 'Tentar novamente';
    SuppressibleMsgBox(
      Mensagem + #13#10 + #13#10 +
      'O assistente continua aberto. Corrija a situação e clique em "Tentar novamente".',
      mbInformation,
      MB_OK,
      IDOK);
    Result := False;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpReady then
  begin
    if TrayEstaEmExecucao then
    begin
      WizardForm.NextButton.Caption := 'Tentar novamente';
    end
    else
    begin
      WizardForm.NextButton.Caption := 'Instalar';
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Mensagem: String;
begin
  Result := '';
  if not ValidarProntidaoParaInstalar(Mensagem) then
  begin
    Result := Mensagem;
  end;
end;

function DeveAbrirAposInstalacaoNova: Boolean;
begin
  Result := (ModoInstalacao = miInstalar) and
    not InstalacaoAnteriorDetectada and
    not DowngradeDetectado and
    not TrayEstaEmExecucao;
  if not Result then
  begin
    RegistrarDiagnostico('INFO', 'Tray não foi aberto automaticamente porque a ação foi ' + NomeAcao + '.');
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and
     RegKeyExists(HKLM, ChaveDesinstalacao) and
     RegKeyExists(HKCU, ChaveDesinstalacao) then
  begin
    if RegDeleteKeyIncludingSubkeys(HKCU, ChaveDesinstalacao) then
    begin
      RegistrarDiagnostico('INFO', 'Registro de desinstalação legado por usuário foi migrado para a instalação elevada.');
    end;
  end;
end;

function InitializeUninstall: Boolean;
var
  EstadoWorker: TEstadoWorker;
begin
  Result := False;
  if TrayEstaEmExecucao and not AguardarEncerramentoDoTray then
  begin
    SuppressibleMsgBox(
      'O Anamnesis continua aberto. Feche-o pela bandeja do Windows antes de desinstalar. ' +
      'Se houver uma gravação ativa, finalize-a primeiro. Nenhum processo foi encerrado à força.',
      mbError,
      MB_OK,
      IDOK);
    Exit;
  end;

  EstadoWorker := ConsultarEstadoWorker;
  if EstadoWorker = ewExecutando then
  begin
    SuppressibleMsgBox(
      'O Anamnesis ainda está processando uma reunião. Aguarde o Worker terminar antes de desinstalar.',
      mbError,
      MB_OK,
      IDOK);
    Exit;
  end;

  if EstadoWorker = ewDesconhecido then
  begin
    SuppressibleMsgBox(
      'Não foi possível confirmar se o Worker está em execução. A desinstalação foi cancelada por segurança.',
      mbError,
      MB_OK,
      IDOK);
    Exit;
  end;

  Result := True;
end;
