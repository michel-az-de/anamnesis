using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class ReleaseScriptTests
{
    [Fact]
    public void BuildCanonicoDeveRecusarArvoreDeTrabalhoSujaPorPadrao()
    {
        var raiz = EncontrarRaizRepositorio();
        var build = File.ReadAllText(Path.Combine(raiz, "scripts", "Build-Installer.ps1"));

        Assert.Contains("PermitirArvoreDeTrabalhoSuja", build, StringComparison.Ordinal);
        Assert.Contains("git -C $repositorio status --porcelain", build, StringComparison.Ordinal);
        Assert.Contains("arvoreDeTrabalhoLimpa", build, StringComparison.Ordinal);
        Assert.Contains("rev-parse HEAD", build, StringComparison.Ordinal);
        Assert.DoesNotContain("rev-parse --short HEAD", build, StringComparison.Ordinal);
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
}
