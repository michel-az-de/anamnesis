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
