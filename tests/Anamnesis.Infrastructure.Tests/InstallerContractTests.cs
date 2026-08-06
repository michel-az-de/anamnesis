using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed partial class InstallerContractTests
{
    [Fact]
    public void InstaladorDeveEntregarUmUnicoAplicativoWindowsIdentificado()
    {
        var raiz = EncontrarRaizRepositorio();
        var inno = File.ReadAllText(Path.Combine(raiz, "installer", "Anamnesis.iss"));
        var projetoTray = File.ReadAllText(Path.Combine(
            raiz,
            "src",
            "Anamnesis.Tray",
            "Anamnesis.Tray.csproj"));

        Assert.Contains("SetupIconFile={#SourceRoot}\\tray\\Anamnesis.ico", inno, StringComparison.Ordinal);
        Assert.Contains("Name: \"startup\"", inno, StringComparison.Ordinal);
        Assert.Contains("--background", inno, StringComparison.Ordinal);
        Assert.Contains(
            "Name: \"{userprograms}\\Anamnesis\\Anamnesis\"",
            inno,
            StringComparison.Ordinal);
        Assert.Contains("Name: \"{userdesktop}\\Anamnesis\"", inno, StringComparison.Ordinal);
        Assert.DoesNotContain("Name: \"{group}\\Anamnesis\"", inno, StringComparison.Ordinal);
        Assert.DoesNotContain("{autodesktop}", inno, StringComparison.Ordinal);
        Assert.DoesNotContain("Anamnesis Worker", inno, StringComparison.Ordinal);
        Assert.Single(AtalhosMenuIniciar().Matches(inno).Cast<Match>());
        Assert.Contains("<ApplicationIcon>Assets\\Anamnesis.ico</ApplicationIcon>", projetoTray, StringComparison.Ordinal);
        Assert.Contains(
            "<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>",
            projetoTray,
            StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            raiz,
            "src",
            "Anamnesis.Tray",
            "Assets",
            "Anamnesis.ico")));
    }

    [Fact]
    public void SmokeDoInstaladorDeveRecusarSobrescreverInstalacaoReal()
    {
        var script = File.ReadAllText(Path.Combine(
            EncontrarRaizRepositorio(),
            "scripts",
            "Test-Installer.ps1"));

        Assert.Contains("registroProdutoInstalado", script, StringComparison.Ordinal);
        Assert.Contains("instalacao real do Anamnesis", script, StringComparison.Ordinal);
        Assert.Contains("ANAMNESIS_DIRETORIO_DADOS", script, StringComparison.Ordinal);
        Assert.Contains("atalhoInstalado", script, StringComparison.Ordinal);
        Assert.Contains("configuracaoCriada", script, StringComparison.Ordinal);
        Assert.Contains("desinstalacao.log", script, StringComparison.Ordinal);
        Assert.Contains("Get-ValorRegistroOpcional", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Get-ItemPropertyValue", script, StringComparison.Ordinal);
        Assert.DoesNotContain("/NOICONS", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InstaladorElevadoDeveManterAtalhosNoPerfilDoUsuario()
    {
        var raiz = EncontrarRaizRepositorio();
        var inno = File.ReadAllText(Path.Combine(raiz, "installer", "Anamnesis.iss"));
        var script = File.ReadAllText(Path.Combine(raiz, "scripts", "Test-Installer.ps1"));

        Assert.DoesNotContain("/GROUP=", script, StringComparison.Ordinal);
        Assert.Contains("DisableProgramGroupPage=yes", inno, StringComparison.Ordinal);
        Assert.Contains("SpecialFolder]::Programs", script, StringComparison.Ordinal);
        Assert.Contains("SpecialFolder]::CommonPrograms", script, StringComparison.Ordinal);
        Assert.Contains("Anamnesis\\Anamnesis.lnk", script, StringComparison.Ordinal);
        Assert.Contains("atalhoMenuIniciarComum", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InstaladorDeveOrientarInstalacaoAtualizacaoEReparoComTermosEIdentidadeVisual()
    {
        var raiz = EncontrarRaizRepositorio();
        var inno = File.ReadAllText(Path.Combine(raiz, "installer", "Anamnesis.iss"));
        var termos = File.ReadAllText(Path.Combine(raiz, "installer", "Termos-de-Uso.txt"));
        var projetoTray = File.ReadAllText(Path.Combine(
            raiz,
            "src",
            "Anamnesis.Tray",
            "Anamnesis.Tray.csproj"));

        Assert.Contains("LicenseFile=Termos-de-Uso.txt", inno, StringComparison.Ordinal);
        Assert.Contains("TModoInstalacao", inno, StringComparison.Ordinal);
        Assert.Contains("miInstalar", inno, StringComparison.Ordinal);
        Assert.Contains("miAtualizar", inno, StringComparison.Ordinal);
        Assert.Contains("miReparar", inno, StringComparison.Ordinal);
        Assert.Contains("UsePreviousAppDir=yes", inno, StringComparison.Ordinal);
        Assert.Contains("WizardStyle=modern dynamic", inno, StringComparison.Ordinal);
        Assert.Contains("WizardImageFile={#SourceRoot}\\tray\\Anamnesis.png", inno, StringComparison.Ordinal);
        Assert.Contains("WizardImageBackColor=$10172E", inno, StringComparison.Ordinal);
        Assert.Contains("SolicitarEncerramentoDoTray", inno, StringComparison.Ordinal);
        Assert.Contains("--encerrar-para-atualizacao", inno, StringComparison.Ordinal);
        Assert.Contains("CheckForMutexes", inno, StringComparison.Ordinal);
        Assert.Contains("CloseApplications=no", inno, StringComparison.Ordinal);
        Assert.DoesNotContain("CloseApplications=yes", inno, StringComparison.Ordinal);
        Assert.Contains("UninstallDisplayIcon={app}\\tray\\Anamnesis.ico", inno, StringComparison.Ordinal);
        Assert.Contains("IconFilename: \"{app}\\tray\\Anamnesis.ico\"", inno, StringComparison.Ordinal);
        Assert.Contains("Termos simples de uso", termos, StringComparison.Ordinal);
        Assert.Contains("gravações", termos, StringComparison.Ordinal);
        Assert.Contains("licença MIT", termos, StringComparison.Ordinal);
        Assert.Contains("Assets\\Anamnesis.png", projetoTray, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeDoInstaladorDeveValidarReparoEAtualizacaoIsolados()
    {
        var script = File.ReadAllText(Path.Combine(
            EncontrarRaizRepositorio(),
            "scripts",
            "Test-Installer.ps1"));

        Assert.Contains("UpdateInstallerPath", script, StringComparison.Ordinal);
        Assert.Contains("reparo.log", script, StringComparison.Ordinal);
        Assert.Contains("atualizacao.log", script, StringComparison.Ordinal);
        Assert.Contains("codigoReparo", script, StringComparison.Ordinal);
        Assert.Contains("codigoAtualizacao", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeDeveRedescobrirRegistroAposMigracaoElevada()
    {
        var script = File.ReadAllText(Path.Combine(
            EncontrarRaizRepositorio(),
            "scripts",
            "Test-Installer.ps1"));

        var atualizacao = script.IndexOf(
            "$processoAtualizacao = Start-Process -FilePath $atualizador",
            StringComparison.Ordinal);
        var codigoAtualizacao = script.IndexOf(
            "$codigoAtualizacao = $processoAtualizacao.ExitCode",
            atualizacao,
            StringComparison.Ordinal);
        var redescoberta = script.IndexOf(
            "$registroProdutoInstalado = Get-CaminhoRegistroProdutoInstalado",
            codigoAtualizacao,
            StringComparison.Ordinal);
        var leituraVersao = script.IndexOf(
            "$versaoAtualizada = Get-ValorRegistroOpcional",
            codigoAtualizacao,
            StringComparison.Ordinal);

        Assert.True(atualizacao >= 0);
        Assert.True(codigoAtualizacao > atualizacao);
        Assert.True(redescoberta > codigoAtualizacao);
        Assert.True(leituraVersao > redescoberta);
    }

    [Fact]
    public void SmokeDevePreservarTrayLegadoESoRepararComTrayAtualCooperativo()
    {
        var script = File.ReadAllText(Path.Combine(
            EncontrarRaizRepositorio(),
            "scripts",
            "Test-Installer.ps1"));

        var tentativaLegada = script.IndexOf(
            "$tentativaAtualizacaoComTrayLegado = Start-Process -FilePath $atualizador",
            StringComparison.Ordinal);
        var definicaoAssertLog = script.IndexOf(
            "function Assert-LogContem",
            StringComparison.Ordinal);
        var primeiroUsoAssertLog = tentativaLegada < 0
            ? -1
            : script.IndexOf("Assert-LogContem `", tentativaLegada, StringComparison.Ordinal);
        var fechamentoControlado = tentativaLegada < 0
            ? -1
            : script.IndexOf(
                "Stop-Process -Id $tray.Id -Force",
                tentativaLegada,
                StringComparison.Ordinal);
        var atualizacao = fechamentoControlado < 0
            ? -1
            : script.IndexOf(
                "$processoAtualizacao = Start-Process -FilePath $atualizador",
                fechamentoControlado,
                StringComparison.Ordinal);
        var reparoAtual = atualizacao < 0
            ? -1
            : script.IndexOf(
                "$reparoPayloadIncompleto = Start-Process -FilePath $atualizador",
                atualizacao,
                StringComparison.Ordinal);

        Assert.True(tentativaLegada >= 0);
        Assert.True(definicaoAssertLog >= 0 && definicaoAssertLog < primeiroUsoAssertLog);
        Assert.True(fechamentoControlado > tentativaLegada);
        Assert.True(atualizacao > fechamentoControlado);
        Assert.True(reparoAtual > atualizacao);
        Assert.Contains("atualizacao-tray-legado-bloqueada.log", script, StringComparison.Ordinal);
        Assert.Contains("trayLegadoPreservado", script, StringComparison.Ordinal);
        Assert.Contains("encerramentoCooperativoReparo", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeDeveUsarProbeModernoInferiorParaTestarDowngrade()
    {
        var raiz = EncontrarRaizRepositorio();
        var caminhoBuildProbe = Path.Combine(raiz, "scripts", "Build-DowngradeProbe.ps1");
        var smoke = File.ReadAllText(Path.Combine(raiz, "scripts", "Test-Installer.ps1"));
        var github = File.ReadAllText(Path.Combine(
            raiz,
            ".github",
            "workflows",
            "beta-installer.yml"));

        Assert.True(File.Exists(caminhoBuildProbe));
        var buildProbe = File.ReadAllText(caminhoBuildProbe);

        Assert.Contains("DowngradeInstallerPath", smoke, StringComparison.Ordinal);
        Assert.Contains("downgrade-probe.json", smoke, StringComparison.Ordinal);
        Assert.Contains("publicavel", smoke, StringComparison.Ordinal);
        Assert.Equal(
            2,
            Regex.Count(
                smoke,
                @"Start-Process -FilePath \$instaladorDowngrade"));
        Assert.DoesNotContain(
            "$tentativaDowngrade = Start-Process -FilePath $instalador ",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains("downgrade autom", smoke, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Build-DowngradeProbe.ps1", github, StringComparison.Ordinal);
        Assert.Contains(
            "-DowngradeInstallerPath $env:ANAMNESIS_DOWNGRADE_INSTALLER",
            github,
            StringComparison.Ordinal);
        Assert.Contains("payload", buildProbe, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("release.json", buildProbe, StringComparison.Ordinal);
        Assert.Contains("[Version]", buildProbe, StringComparison.Ordinal);
        Assert.Contains("deve ficar fora da publicacao canonica", buildProbe, StringComparison.Ordinal);
        Assert.Contains("ProductVersion", buildProbe, StringComparison.Ordinal);
        Assert.Contains("sha256", buildProbe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Publish-Alpha.ps1", buildProbe, StringComparison.Ordinal);
        Assert.DoesNotContain("Build-Installer.ps1", buildProbe, StringComparison.Ordinal);
    }

    [Fact]
    public void DowngradeSilenciosoDeveRegistrarCausaNoPrimeiroRamoDeBloqueio()
    {
        var raiz = EncontrarRaizRepositorio();
        var inno = File.ReadAllText(Path.Combine(raiz, "installer", "Anamnesis.iss"));
        var inicioNextButton = inno.IndexOf(
            "function NextButtonClick",
            StringComparison.Ordinal);
        var inicioBloqueio = inicioNextButton < 0
            ? -1
            : inno.IndexOf(
                "if (CurPageID = PaginaAcao.ID) and DowngradeDetectado then",
                inicioNextButton,
                StringComparison.Ordinal);
        var fimBloqueio = inicioBloqueio < 0
            ? -1
            : inno.IndexOf("Result := False;", inicioBloqueio, StringComparison.Ordinal);

        Assert.True(inicioNextButton >= 0);
        Assert.True(inicioBloqueio >= inicioNextButton);
        Assert.True(fimBloqueio > inicioBloqueio);

        var ramoBloqueio = inno[inicioBloqueio..fimBloqueio];
        var registroDiagnostico = ramoBloqueio.IndexOf(
            "RegistrarDiagnostico",
            StringComparison.Ordinal);
        var mensagemUsuario = ramoBloqueio.IndexOf(
            "SuppressibleMsgBox",
            StringComparison.Ordinal);

        Assert.True(registroDiagnostico >= 0);
        Assert.True(mensagemUsuario > registroDiagnostico);
        Assert.Contains(
            "downgrade autom",
            ramoBloqueio[..mensagemUsuario],
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AtualizacaoDeveVersionarEValidarOsBinariosReais()
    {
        var raiz = EncontrarRaizRepositorio();
        var build = File.ReadAllText(Path.Combine(raiz, "scripts", "Build-Installer.ps1"));
        var publish = File.ReadAllText(Path.Combine(raiz, "scripts", "Publish-Alpha.ps1"));
        var smoke = File.ReadAllText(Path.Combine(raiz, "scripts", "Test-Installer.ps1"));

        Assert.Contains("-Version $Version", build, StringComparison.Ordinal);
        Assert.Contains("-NumericVersion $NumericVersion", build, StringComparison.Ordinal);
        Assert.Contains("[string]$Version", publish, StringComparison.Ordinal);
        Assert.Contains("[string]$NumericVersion", publish, StringComparison.Ordinal);
        Assert.Contains("-p:Version=$Version", publish, StringComparison.Ordinal);
        Assert.Contains("-p:FileVersion=$NumericVersion", publish, StringComparison.Ordinal);
        Assert.Contains(
            "-p:IncludeSourceRevisionInInformationalVersion=false",
            publish,
            StringComparison.Ordinal);
        Assert.Contains("caminhoTray", smoke, StringComparison.Ordinal);
        Assert.Contains("caminhoWorker", smoke, StringComparison.Ordinal);
        Assert.Contains("ProductVersion", smoke, StringComparison.Ordinal);
        Assert.Contains("versoesBinariosAtualizados", smoke, StringComparison.Ordinal);
    }

    [Fact]
    public void ReparoAtualizacaoEDesinstalacaoDevemSerCooperativosEPreservarDados()
    {
        var raiz = EncontrarRaizRepositorio();
        var inno = File.ReadAllText(Path.Combine(raiz, "installer", "Anamnesis.iss"));
        var script = File.ReadAllText(Path.Combine(raiz, "scripts", "Test-Installer.ps1"));

        Assert.Contains("InitializeUninstall", inno, StringComparison.Ordinal);
        Assert.Contains("ConsultarEstadoWorker", inno, StringComparison.Ordinal);
        Assert.Contains("ewDesconhecido", inno, StringComparison.Ordinal);
        Assert.Contains("DowngradeDetectado", inno, StringComparison.Ordinal);
        Assert.Contains("codigoDowngradeBloqueado", script, StringComparison.Ordinal);
        Assert.Contains("[InstallDelete]", inno, StringComparison.Ordinal);
        Assert.Contains("DestName: \"LICENSE\"", inno, StringComparison.Ordinal);
        Assert.Contains("encerramentoCooperativoDesinstalacao", script, StringComparison.Ordinal);
        Assert.Contains("hashConfiguracaoAntes", script, StringComparison.Ordinal);
        Assert.Contains("reuniaoSentinela", script, StringComparison.Ordinal);
        Assert.Contains("atalhoPreservado", script, StringComparison.Ordinal);
        Assert.Contains("downgradeComPayloadIncompletoBloqueado", script, StringComparison.Ordinal);
        Assert.Contains("reparoPayloadIncompleto", script, StringComparison.Ordinal);
    }

    [Fact]
    public void DowngradeDeveSerBloqueadoAntesDeClassificarPayloadIncompletoComoReparo()
    {
        var raiz = EncontrarRaizRepositorio();
        var inno = File.ReadAllText(Path.Combine(raiz, "installer", "Anamnesis.iss"));
        var inicio = inno.IndexOf("procedure DeterminarModoInstalacao;", StringComparison.Ordinal);
        var fim = inno.IndexOf("function NomeAcao: String;", inicio, StringComparison.Ordinal);

        Assert.True(inicio >= 0 && fim > inicio);
        var determinarModo = inno[inicio..fim];
        var compararVersoes = determinarModo.IndexOf("CompararVersoesDisponiveis", StringComparison.Ordinal);
        var verificarPayload = determinarModo.IndexOf("PayloadObrigatorioExiste", StringComparison.Ordinal);

        Assert.True(compararVersoes >= 0);
        Assert.True(verificarPayload > compararVersoes);
        Assert.Contains("ExecutavelTray", inno, StringComparison.Ordinal);
        Assert.Contains("ExecutavelWorker", inno, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerLegadoNaoDeveBloquearAtualizacaoBaseadaNoTray()
    {
        var raiz = EncontrarRaizRepositorio();
        var inno = File.ReadAllText(Path.Combine(raiz, "installer", "Anamnesis.iss"));
        var inicioComparacao = inno.IndexOf("function CompararVersoesDisponiveis(", StringComparison.Ordinal);
        var fimComparacao = inno.IndexOf("procedure DeterminarModoInstalacao;", inicioComparacao, StringComparison.Ordinal);

        Assert.True(inicioComparacao >= 0 && fimComparacao > inicioComparacao);
        var comparacao = inno[inicioComparacao..fimComparacao];

        Assert.Contains("ExecutavelTray", comparacao, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecutavelWorker", comparacao, StringComparison.Ordinal);
        Assert.Contains("WorkerComVersaoDivergente", inno, StringComparison.Ordinal);

        var inicioDivergencia = inno.IndexOf(
            "function WorkerPossuiVersaoDivergente(",
            StringComparison.Ordinal);
        var fimDivergencia = inno.IndexOf(
            "function CompararVersoesDisponiveis(",
            inicioDivergencia,
            StringComparison.Ordinal);

        Assert.True(inicioDivergencia >= 0 && fimDivergencia > inicioDivergencia);
        var divergencia = inno[inicioDivergencia..fimDivergencia];
        Assert.Contains("ExecutavelTray", divergencia, StringComparison.Ordinal);
        Assert.Contains("ExecutavelWorker", divergencia, StringComparison.Ordinal);
        Assert.DoesNotContain("{srcexe}", divergencia, StringComparison.Ordinal);
        Assert.Contains(
            "a versão do Worker diverge do Tray.",
            inno,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "a versão do Worker diverge do Tray e do pacote.",
            inno,
            StringComparison.Ordinal);

        var smoke = File.ReadAllText(Path.Combine(raiz, "scripts", "Test-Installer.ps1"));
        Assert.Contains("workerInicialLegado", smoke, StringComparison.Ordinal);
        Assert.Contains(
            "Worker divergente na release anterior oficial",
            smoke,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeDeveAceitarWorkerLegadoDaReleaseAnteriorSemRelaxarVersaoDoTray()
    {
        var raiz = EncontrarRaizRepositorio();
        var smoke = File.ReadAllText(Path.Combine(raiz, "scripts", "Test-Installer.ps1"));

        Assert.Contains("workerInicialLegado", smoke, StringComparison.Ordinal);
        Assert.Contains(
            "$binarioTrayInstalado.ProductVersion -ne $ExpectedInitialVersion",
            smoke,
            StringComparison.Ordinal);

        var inicioValidacaoTray = smoke.IndexOf(
            "if ($binarioTrayInstalado.ProductVersion -ne $ExpectedInitialVersion",
            StringComparison.Ordinal);
        var fimValidacaoTray = smoke.IndexOf(
            "$workerInicialLegado =",
            inicioValidacaoTray,
            StringComparison.Ordinal);
        Assert.True(inicioValidacaoTray >= 0 && fimValidacaoTray > inicioValidacaoTray);
        var validacaoTray = smoke[inicioValidacaoTray..fimValidacaoTray];
        Assert.DoesNotContain(
            "$binarioWorkerInstalado.ProductVersion",
            validacaoTray,
            StringComparison.Ordinal);
        Assert.Contains("Worker divergente na release anterior oficial", smoke, StringComparison.Ordinal);
    }

    [Fact]
    public void AtualizacaoElevadaDevePreservarStartupDesativadoDaInstalacaoLegada()
    {
        var inno = File.ReadAllText(Path.Combine(
            EncontrarRaizRepositorio(),
            "installer",
            "Anamnesis.iss"));

        var inicioPreservacao = inno.IndexOf(
            "procedure DeterminarPreservacaoTarefaStartup;",
            StringComparison.Ordinal);
        var fimPreservacao = inicioPreservacao < 0
            ? -1
            : inno.IndexOf("procedure InitializeWizard;", inicioPreservacao, StringComparison.Ordinal);

        Assert.True(inicioPreservacao >= 0);
        Assert.True(fimPreservacao > inicioPreservacao);
        var preservacao = inno[inicioPreservacao..fimPreservacao];
        Assert.Contains("InstalacaoAnteriorDetectada", preservacao, StringComparison.Ordinal);
        Assert.Contains("RegValueExists(HKCU, ChaveInicializacao, 'Anamnesis')", preservacao, StringComparison.Ordinal);
        Assert.Contains("PreservarStartupDesativado :=", preservacao, StringComparison.Ordinal);
        Assert.DoesNotContain("WizardSelectTasks", preservacao, StringComparison.Ordinal);

        var inicioWizard = inno.IndexOf("procedure InitializeWizard;", StringComparison.Ordinal);
        var fimWizard = inno.IndexOf("function ShouldSkipPage", inicioWizard, StringComparison.Ordinal);
        Assert.True(inicioWizard >= 0 && fimWizard > inicioWizard);
        var wizard = inno[inicioWizard..fimWizard];
        var determinarModo = wizard.IndexOf("DeterminarModoInstalacao;", StringComparison.Ordinal);
        var preservarStartup = wizard.IndexOf(
            "DeterminarPreservacaoTarefaStartup;",
            StringComparison.Ordinal);
        Assert.True(determinarModo >= 0);
        Assert.True(preservarStartup > determinarModo);
        Assert.DoesNotContain("WizardSelectTasks", wizard, StringComparison.Ordinal);

        var inicioMudancaPagina = inno.IndexOf(
            "procedure CurPageChanged(CurPageID: Integer);",
            StringComparison.Ordinal);
        var fimMudancaPagina = inno.IndexOf(
            "function PrepareToInstall",
            inicioMudancaPagina,
            StringComparison.Ordinal);
        Assert.True(inicioMudancaPagina >= 0 && fimMudancaPagina > inicioMudancaPagina);
        var mudancaPagina = inno[inicioMudancaPagina..fimMudancaPagina];
        Assert.Contains("CurPageID = wpSelectTasks", mudancaPagina, StringComparison.Ordinal);
        Assert.Contains("PreservarStartupDesativado", mudancaPagina, StringComparison.Ordinal);
        Assert.Contains("PreservacaoStartupAplicada", mudancaPagina, StringComparison.Ordinal);
        Assert.Contains("WizardSelectTasks('!startup')", mudancaPagina, StringComparison.Ordinal);
        Assert.Contains("WizardIsTaskSelected('startup')", mudancaPagina, StringComparison.Ordinal);

        var inicioPreflight = inno.IndexOf(
            "function ValidarProntidaoParaInstalar",
            StringComparison.Ordinal);
        var fimPreflight = inno.IndexOf(
            "function NextButtonClick",
            inicioPreflight,
            StringComparison.Ordinal);
        Assert.True(inicioPreflight >= 0 && fimPreflight > inicioPreflight);
        var preflight = inno[inicioPreflight..fimPreflight];
        Assert.Contains(
            "PreservarStartupDesativado and not PreservacaoStartupAplicada",
            preflight,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WizardElevadoDeveDiagnosticarVersaoEEvitarAbrirTrayEmReparoOuAtualizacao()
    {
        var raiz = EncontrarRaizRepositorio();
        var inno = File.ReadAllText(Path.Combine(raiz, "installer", "Anamnesis.iss"));

        Assert.Contains("PrivilegesRequired=admin", inno, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivilegesRequired=lowest", inno, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivilegesRequiredOverridesAllowed=commandline", inno, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivilegesRequiredOverridesAllowed=dialog", inno, StringComparison.Ordinal);
        Assert.Contains("CreateInputOptionPage", inno, StringComparison.Ordinal);
        Assert.Contains("PaginaDiagnostico", inno, StringComparison.Ordinal);
        Assert.Contains("Ação recomendada", inno, StringComparison.Ordinal);
        Assert.Contains("RegistrarDiagnostico", inno, StringComparison.Ordinal);
        Assert.Contains("UltimoDiagnostico", inno, StringComparison.Ordinal);
        Assert.Contains("Check: DeveAbrirAposInstalacaoNova", inno, StringComparison.Ordinal);
        Assert.Contains("function DeveAbrirAposInstalacaoNova: Boolean;", inno, StringComparison.Ordinal);
        Assert.Contains("Tentar novamente", inno, StringComparison.Ordinal);
        Assert.Contains("RegKeyExists(HKLM, ChaveDesinstalacao)", inno, StringComparison.Ordinal);
        Assert.Contains("RegDeleteKeyIncludingSubkeys(HKCU, ChaveDesinstalacao)", inno, StringComparison.Ordinal);
        Assert.Contains("Get-CaminhoRegistroProdutoInstalado", File.ReadAllText(Path.Combine(raiz, "scripts", "Test-Installer.ps1")), StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseCanonicoDeveCentralizarVersaoEAutomacao()
    {
        var raiz = EncontrarRaizRepositorio();
        var caminhoVersao = Path.Combine(raiz, "release", "versao.json");

        Assert.True(File.Exists(caminhoVersao));
        using var documento = JsonDocument.Parse(File.ReadAllText(caminhoVersao));
        var raizVersao = documento.RootElement;

        var versao = raizVersao.GetProperty("versao").GetString()
            ?? throw new InvalidDataException("A versao canonica esta ausente.");
        var versaoNumerica = raizVersao.GetProperty("versaoNumerica").GetString()
            ?? throw new InvalidDataException("A versao numerica esta ausente.");
        var canal = raizVersao.GetProperty("canal").GetString()
            ?? throw new InvalidDataException("O canal da release esta ausente.");
        var versaoAnterior = raizVersao.GetProperty("versaoAnteriorParaSmoke").GetString()
            ?? throw new InvalidDataException("A versao anterior esta ausente.");
        var versaoNumericaAnterior = raizVersao
            .GetProperty("versaoNumericaAnteriorParaSmoke")
            .GetString()
            ?? throw new InvalidDataException("A versao numerica anterior esta ausente.");
        var urlInstaladorAnterior = raizVersao
            .GetProperty("urlInstaladorAnterior")
            .GetString()
            ?? throw new InvalidDataException("A URL do instalador anterior esta ausente.");
        var sha256InstaladorAnterior = raizVersao
            .GetProperty("sha256InstaladorAnterior")
            .GetString()
            ?? throw new InvalidDataException("O SHA-256 do instalador anterior esta ausente.");

        Assert.Matches(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$", versao);
        Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", versaoNumerica);
        Assert.False(string.IsNullOrWhiteSpace(canal));
        Assert.Matches(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$", versaoAnterior);
        Assert.True(Version.Parse(versaoNumericaAnterior) < Version.Parse(versaoNumerica));
        Assert.True(Uri.TryCreate(urlInstaladorAnterior, UriKind.Absolute, out var uriAnterior));
        Assert.Equal(Uri.UriSchemeHttps, uriAnterior!.Scheme);
        Assert.EndsWith(
            $"/v{versaoAnterior}/Anamnesis-{versaoAnterior}-win-x64-setup.exe",
            uriAnterior.AbsolutePath,
            StringComparison.Ordinal);
        Assert.Matches("^[0-9a-f]{64}$", sha256InstaladorAnterior);

        var obterVersao = File.ReadAllText(Path.Combine(raiz, "scripts", "Obter-VersaoRelease.ps1"));
        var build = File.ReadAllText(Path.Combine(raiz, "scripts", "Build-Installer.ps1"));
        var publicar = File.ReadAllText(Path.Combine(raiz, "scripts", "Publish-Alpha.ps1"));
        var smoke = File.ReadAllText(Path.Combine(raiz, "scripts", "Test-Installer.ps1"));
        var inno = File.ReadAllText(Path.Combine(raiz, "installer", "Anamnesis.iss"));
        var github = File.ReadAllText(Path.Combine(raiz, ".github", "workflows", "beta-installer.yml"));
        var gitignore = File.ReadAllText(Path.Combine(raiz, ".gitignore"));
        var readme = File.ReadAllText(Path.Combine(raiz, "README.md"));
        var runbook = File.ReadAllText(Path.Combine(raiz, "docs", "release.md"));
        var spek = File.ReadAllText(Path.Combine(
            raiz,
            "30-especificacoes",
            "02-especificacoes",
            "SPEK-049 Release Canonico do Instalador Windows.md"));
        var adr = File.ReadAllText(Path.Combine(
            raiz,
            "30-especificacoes",
            "03-arquitetura",
            "ADR-018 Release Canonico do Instalador Windows.md"));
        var caminhoObterAnterior = Path.Combine(raiz, "scripts", "Obter-InstaladorAnterior.ps1");

        Assert.Contains("release\\versao.json", obterVersao, StringComparison.Ordinal);
        Assert.Contains("urlInstaladorAnterior", obterVersao, StringComparison.Ordinal);
        Assert.Contains("sha256InstaladorAnterior", obterVersao, StringComparison.Ordinal);
        Assert.Contains("Obter-VersaoRelease.ps1", build, StringComparison.Ordinal);
        Assert.Contains("artifacts\\releases\\$Version", build, StringComparison.Ordinal);
        Assert.Contains("release.json", build, StringComparison.Ordinal);
        Assert.DoesNotContain(versao, build, StringComparison.Ordinal);
        Assert.Contains("Obter-VersaoRelease.ps1", publicar, StringComparison.Ordinal);
        Assert.DoesNotContain(versao, publicar, StringComparison.Ordinal);
        Assert.Contains("ExpectedInitialVersion", smoke, StringComparison.Ordinal);
        Assert.Contains("ExpectedInitialNumericVersion", smoke, StringComparison.Ordinal);
        Assert.DoesNotContain(versaoAnterior, smoke, StringComparison.Ordinal);
        Assert.Contains("#error AppVersion deve ser informado", inno, StringComparison.Ordinal);
        Assert.DoesNotContain($"#define AppVersion \"{versao}\"", inno, StringComparison.Ordinal);
        Assert.Contains("Obter-VersaoRelease.ps1", github, StringComparison.Ordinal);
        Assert.Contains("ANAMNESIS_VERSION", github, StringComparison.Ordinal);
        Assert.Contains("-ExpectedInitialVersion $env:ANAMNESIS_PREVIOUS_VERSION", github, StringComparison.Ordinal);
        Assert.Contains("if: success()", github, StringComparison.Ordinal);
        Assert.Contains("diagnostico-falha", github, StringComparison.Ordinal);
        Assert.DoesNotContain("if: always()", github, StringComparison.Ordinal);
        Assert.DoesNotContain(".gitlab-ci.yml", github, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(raiz, ".gitlab-ci.yml")));
        Assert.DoesNotContain("GitLab", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GitLab", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GitLab", spek, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GitLab", adr, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".ci/", gitignore, StringComparison.Ordinal);
        Assert.Contains("docs/release.md", readme, StringComparison.Ordinal);
        Assert.True(File.Exists(caminhoObterAnterior));
    }

    [Fact]
    public void SmokeDeAtualizacaoDeveUsarReleaseAnteriorOficial()
    {
        var raiz = EncontrarRaizRepositorio();
        var github = File.ReadAllText(Path.Combine(raiz, ".github", "workflows", "beta-installer.yml"));
        var caminhoScript = Path.Combine(raiz, "scripts", "Obter-InstaladorAnterior.ps1");

        Assert.True(File.Exists(caminhoScript));
        var script = File.ReadAllText(caminhoScript);

        Assert.Contains("UrlInstaladorAnterior", script, StringComparison.Ordinal);
        Assert.Contains("Sha256InstaladorAnterior", script, StringComparison.Ordinal);
        Assert.Contains("Invoke-WebRequest", script, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", script, StringComparison.Ordinal);
        Assert.Contains(".download", script, StringComparison.Ordinal);

        Assert.Contains("Obter-InstaladorAnterior.ps1", github, StringComparison.Ordinal);
        Assert.DoesNotContain("Construir versao anterior", github, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("-Version $env:ANAMNESIS_PREVIOUS_VERSION", github, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(github, @"Build-Installer\.ps1"));
    }

    [Fact]
    public void GitHubDevePromoverReleaseImutavelSomenteDepoisDoSmoke()
    {
        var raiz = EncontrarRaizRepositorio();
        var github = File.ReadAllText(Path.Combine(raiz, ".github", "workflows", "beta-installer.yml"));

        Assert.Contains("candidato-anamnesis-", github, StringComparison.Ordinal);
        Assert.Contains("publicar-release-github", github, StringComparison.Ordinal);
        Assert.Contains("if: startsWith(github.ref, 'refs/tags/v')", github, StringComparison.Ordinal);
        Assert.Contains("contents: write", github, StringComparison.Ordinal);
        Assert.Contains("isImmutable", github, StringComparison.Ordinal);
        Assert.Contains("gh release create", github, StringComparison.Ordinal);
        Assert.Contains("gh release delete", github, StringComparison.Ordinal);
        Assert.DoesNotContain("--cleanup-tag", github, StringComparison.Ordinal);
        Assert.Contains("--verify-tag", github, StringComparison.Ordinal);
        Assert.Contains("gh release verify", github, StringComparison.Ordinal);
        Assert.Contains("gh release verify-asset", github, StringComparison.Ordinal);
        Assert.Contains("linhaSomas", github, StringComparison.Ordinal);
        Assert.Contains(
            "actions/download-artifact@3e5f45b2cfb9172054b4087a40e8e0b5a5461e7c",
            github,
            StringComparison.Ordinal);

        var inicioValidacaoAusencia = github.IndexOf(
            "- name: Validar manifesto e ausencia de release anterior",
            StringComparison.Ordinal);
        var inicioPromocao = github.IndexOf(
            "- name: Promover release imutavel",
            StringComparison.Ordinal);
        var validacaoAusencia = github[inicioValidacaoAusencia..inicioPromocao];

        Assert.Contains(
            "gh release list --repo $env:GITHUB_REPOSITORY --limit 1000 --json tagName",
            validacaoAusencia,
            StringComparison.Ordinal);
        Assert.Contains(
            "$tagsExistentes -contains $env:ANAMNESIS_TAG",
            validacaoAusencia,
            StringComparison.Ordinal);
        Assert.DoesNotContain("gh release view", validacaoAusencia, StringComparison.Ordinal);
        Assert.DoesNotContain("Publicar instalador aprovado e evidencias", github, StringComparison.Ordinal);
    }

    [Fact]
    public void GitHubDeveSerUnicaAutoridadeDeRelease()
    {
        var raiz = EncontrarRaizRepositorio();
        var github = File.ReadAllText(Path.Combine(raiz, ".github", "workflows", "beta-installer.yml"));
        var readme = File.ReadAllText(Path.Combine(raiz, "README.md"));
        var runbook = File.ReadAllText(Path.Combine(raiz, "docs", "release.md"));
        var spek = File.ReadAllText(Path.Combine(
            raiz,
            "30-especificacoes",
            "02-especificacoes",
            "SPEK-049 Release Canonico do Instalador Windows.md"));
        var adr = File.ReadAllText(Path.Combine(
            raiz,
            "30-especificacoes",
            "03-arquitetura",
            "ADR-018 Release Canonico do Instalador Windows.md"));

        Assert.False(File.Exists(Path.Combine(raiz, ".gitlab-ci.yml")));
        Assert.DoesNotContain(".gitlab-ci.yml", github, StringComparison.Ordinal);
        Assert.Contains("publicar-release-github", github, StringComparison.Ordinal);
        Assert.Contains("GitHub Release", runbook, StringComparison.Ordinal);
        Assert.DoesNotContain("GitLab", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GitLab", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GitLab", spek, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GitLab", adr, StringComparison.OrdinalIgnoreCase);
    }

    private static string EncontrarRaizRepositorio()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);
        while (atual is not null && !File.Exists(Path.Combine(atual.FullName, "Anamnesis.sln")))
        {
            atual = atual.Parent;
        }

        return atual?.FullName
            ?? throw new DirectoryNotFoundException("A raiz do repositorio nao foi encontrada.");
    }

    [GeneratedRegex("^Name: \\\"\\{userprograms\\}\\\\Anamnesis\\\\Anamnesis\\\"", RegexOptions.Multiline)]
    private static partial Regex AtalhosMenuIniciar();
}
