using System.Diagnostics;
using Anamnesis.Infrastructure.Arquivos;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class WindowsArtefatoLauncherTests
{
    [Fact]
    public async Task DeveAbrirSomenteArquivoExistenteComExtensaoPermitida()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"ata-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(caminho, "# Ata");
        try
        {
            ProcessStartInfo? capturado = null;
            var launcher = new WindowsArtefatoLauncher(inicio => capturado = inicio);

            await launcher.AbrirArquivoAsync(caminho, CancellationToken.None);

            Assert.NotNull(capturado);
            Assert.Equal(Path.GetFullPath(caminho), capturado!.FileName);
            Assert.True(capturado.UseShellExecute);
            Assert.Empty(capturado.ArgumentList);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    [Fact]
    public async Task DeveMostrarArquivoNoExplorerSemConcatenarArgumentos()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"transcricao-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(caminho, "Texto");
        try
        {
            ProcessStartInfo? capturado = null;
            var launcher = new WindowsArtefatoLauncher(inicio => capturado = inicio);

            await launcher.MostrarNaPastaAsync(caminho, CancellationToken.None);

            Assert.NotNull(capturado);
            Assert.Equal("explorer.exe", capturado!.FileName);
            Assert.Equal(["/select,", Path.GetFullPath(caminho)], capturado.ArgumentList);
        }
        finally
        {
            File.Delete(caminho);
        }
    }

    [Fact]
    public async Task DeveRejeitarExecutavelEPastaAusenteSemIniciarProcesso()
    {
        var caminhoExecutavel = Path.Combine(Path.GetTempPath(), $"perigoso-{Guid.NewGuid():N}.exe");
        await File.WriteAllTextAsync(caminhoExecutavel, "nao executar");
        var chamadas = 0;
        var launcher = new WindowsArtefatoLauncher(_ => chamadas++);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                launcher.AbrirArquivoAsync(caminhoExecutavel, CancellationToken.None));
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                launcher.MostrarNaPastaAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), CancellationToken.None));

            Assert.Equal(0, chamadas);
        }
        finally
        {
            File.Delete(caminhoExecutavel);
        }
    }
}
