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
        Assert.Contains("Name: \"{group}\\Anamnesis\"", inno, StringComparison.Ordinal);
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
    public void SmokeDevePoderIsolarOGrupoDoMenuIniciar()
    {
        var raiz = EncontrarRaizRepositorio();
        var inno = File.ReadAllText(Path.Combine(raiz, "installer", "Anamnesis.iss"));
        var script = File.ReadAllText(Path.Combine(raiz, "scripts", "Test-Installer.ps1"));

        Assert.Contains("/GROUP=$grupoAtalhos", script, StringComparison.Ordinal);
        Assert.Contains("DisableProgramGroupPage=auto", inno, StringComparison.Ordinal);
        Assert.DoesNotContain("DisableProgramGroupPage=yes", inno, StringComparison.Ordinal);
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
        Assert.Contains("caminhoWorkerLegado", smoke, StringComparison.Ordinal);
        Assert.Contains("Worker legado", smoke, StringComparison.Ordinal);
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

    [GeneratedRegex("^Name: \\\"\\{group\\}\\\\Anamnesis(?:\\\\|\\\")", RegexOptions.Multiline)]
    private static partial Regex AtalhosMenuIniciar();
}
