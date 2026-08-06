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
        var gitlab = File.ReadAllText(Path.Combine(raiz, ".gitlab-ci.yml"));
        var gitignore = File.ReadAllText(Path.Combine(raiz, ".gitignore"));
        var readme = File.ReadAllText(Path.Combine(raiz, "README.md"));
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
        Assert.Contains("Obter-VersaoRelease.ps1", gitlab, StringComparison.Ordinal);
        Assert.Contains("windows-release", gitlab, StringComparison.Ordinal);
        Assert.Contains("resource_group: anamnesis-release-windows", gitlab, StringComparison.Ordinal);
        Assert.Contains("packages/generic/anamnesis-windows", gitlab, StringComparison.Ordinal);
        Assert.Contains(".ci/", gitignore, StringComparison.Ordinal);
        Assert.Contains("docs/release.md", readme, StringComparison.Ordinal);
        Assert.True(File.Exists(caminhoObterAnterior));
    }

    [Fact]
    public void SmokeDeAtualizacaoDeveUsarReleaseAnteriorOficial()
    {
        var raiz = EncontrarRaizRepositorio();
        var github = File.ReadAllText(Path.Combine(raiz, ".github", "workflows", "beta-installer.yml"));
        var gitlab = File.ReadAllText(Path.Combine(raiz, ".gitlab-ci.yml"));
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

        Assert.DoesNotContain("Build-Installer.ps1", gitlab, StringComparison.Ordinal);
        Assert.DoesNotContain("Test-Installer.ps1", gitlab, StringComparison.Ordinal);
        Assert.DoesNotContain("Instalar-InnoSetup.ps1", gitlab, StringComparison.Ordinal);
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
        Assert.DoesNotContain("Publicar instalador aprovado e evidencias", github, StringComparison.Ordinal);
    }

    [Fact]
    public void GitLabDeveEspelharBytesOficiaisSomenteEmTagProtegida()
    {
        var raiz = EncontrarRaizRepositorio();
        var gitlab = File.ReadAllText(Path.Combine(raiz, ".gitlab-ci.yml"));

        Assert.DoesNotContain("merge_request_event", gitlab, StringComparison.Ordinal);
        Assert.Contains("$CI_COMMIT_REF_PROTECTED == \"true\"", gitlab, StringComparison.Ordinal);
        Assert.Contains("windows-release", gitlab, StringComparison.Ordinal);
        Assert.Contains("github.com/michel-az-de/anamnesis/releases/download", gitlab, StringComparison.Ordinal);
        Assert.Contains("/packages/generic/", gitlab, StringComparison.Ordinal);
        Assert.Contains("CI_JOB_TOKEN", gitlab, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", gitlab, StringComparison.Ordinal);
        Assert.Contains("interruptible: false", gitlab, StringComparison.Ordinal);
        Assert.Contains("hashExistente", gitlab, StringComparison.Ordinal);
        Assert.Contains("release.json", gitlab, StringComparison.Ordinal);
        Assert.Contains("resource_group: anamnesis-release-windows", gitlab, StringComparison.Ordinal);
        Assert.DoesNotContain("Build-Installer.ps1", gitlab, StringComparison.Ordinal);
        Assert.DoesNotContain("Test-Installer.ps1", gitlab, StringComparison.Ordinal);
        Assert.DoesNotContain("Instalar-InnoSetup.ps1", gitlab, StringComparison.Ordinal);
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
