using System.Diagnostics;
using Anamnesis.Infrastructure.Whisper;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class DockerProcessPreflightTests
{
    [Fact]
    public async Task NaoDeveIniciarDesktopQuandoEngineJaEstaDisponivel()
    {
        var iniciou = false;
        var preflight = CriarPreflight(
            "C:\\nao-usado\\Docker Desktop.exe",
            _ => Task.FromResult(true),
            _ => iniciou = true);

        await preflight.PrepararAsync(CancellationToken.None);

        Assert.False(iniciou);
    }

    [Fact]
    public async Task DeveIniciarDockerDesktopOcultoEAguardarEngine()
    {
        var executavel = Path.GetTempFileName();
        try
        {
            var disponibilidade = new Queue<bool>([false, false, true]);
            ProcessStartInfo? inicioCapturado = null;
            var preflight = CriarPreflight(
                executavel,
                _ => Task.FromResult(disponibilidade.Dequeue()),
                inicio => inicioCapturado = inicio,
                maximoTentativas: 2);

            await preflight.PrepararAsync(CancellationToken.None);

            Assert.NotNull(inicioCapturado);
            Assert.Equal(Path.GetFullPath(executavel), inicioCapturado.FileName);
            Assert.True(inicioCapturado.CreateNoWindow);
            Assert.Equal(ProcessWindowStyle.Hidden, inicioCapturado.WindowStyle);
        }
        finally
        {
            File.Delete(executavel);
        }
    }

    [Fact]
    public async Task DeveExplicarExecutavelAusenteETimeout()
    {
        var ausente = CriarPreflight(
            "C:\\ausente\\Docker Desktop.exe",
            _ => Task.FromResult(false),
            _ => throw new Xunit.Sdk.XunitException("Não deveria iniciar."));
        var excecaoAusente = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            ausente.PrepararAsync(CancellationToken.None));
        Assert.Contains("Docker Desktop", excecaoAusente.Message, StringComparison.Ordinal);

        var executavel = Path.GetTempFileName();
        try
        {
            var timeout = CriarPreflight(
                executavel,
                _ => Task.FromResult(false),
                _ => { },
                maximoTentativas: 2);
            var excecaoTimeout = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                timeout.PrepararAsync(CancellationToken.None));
            // A mensagem reflete os parâmetros reais da espera, e não um texto fixo.
            Assert.Contains("2 tentativas", excecaoTimeout.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(executavel);
        }
    }

    private static DockerProcessPreflight CriarPreflight(
        string caminhoDesktop,
        Func<CancellationToken, Task<bool>> verificarEngine,
        Action<ProcessStartInfo> iniciarProcesso,
        int maximoTentativas = 1) =>
        new(
            "C:\\docker\\docker.exe",
            caminhoDesktop,
            verificarEngine,
            iniciarProcesso,
            maximoTentativas,
            TimeSpan.Zero);
}
