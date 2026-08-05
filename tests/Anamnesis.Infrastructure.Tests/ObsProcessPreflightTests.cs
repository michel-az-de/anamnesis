using System.Diagnostics;
using Anamnesis.Infrastructure.Obs;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class ObsProcessPreflightTests
{
    [Fact]
    public async Task NaoDeveIniciarOutroProcessoQuandoObsJaEstaDisponivel()
    {
        var iniciouProcesso = false;
        var preflight = CriarPreflight(
            caminhoExecutavel: "C:\\nao-sera-usado\\obs64.exe",
            verificarDisponibilidade: (_, _) => Task.FromResult(true),
            iniciarProcesso: _ => iniciouProcesso = true);

        await preflight.PrepararAsync(CancellationToken.None);

        Assert.False(iniciouProcesso);
    }

    [Fact]
    public async Task DeveIniciarObsMinimizadoEAguardarWebsocket()
    {
        var executavel = Path.GetTempFileName();
        try
        {
            var disponibilidade = new Queue<bool>([false, false, true]);
            ProcessStartInfo? inicioCapturado = null;
            var preflight = CriarPreflight(
                executavel,
                (_, _) => Task.FromResult(disponibilidade.Dequeue()),
                inicio => inicioCapturado = inicio,
                maximoTentativas: 2);

            await preflight.PrepararAsync(CancellationToken.None);

            Assert.NotNull(inicioCapturado);
            Assert.Equal(Path.GetFullPath(executavel), inicioCapturado.FileName);
            Assert.Equal(Path.GetDirectoryName(Path.GetFullPath(executavel)), inicioCapturado.WorkingDirectory);
            Assert.Contains("--minimize-to-tray", inicioCapturado.ArgumentList);
        }
        finally
        {
            File.Delete(executavel);
        }
    }

    [Fact]
    public async Task DeveExplicarQuandoExecutavelNaoExiste()
    {
        var preflight = CriarPreflight(
            "C:\\ausente\\obs64.exe",
            (_, _) => Task.FromResult(false),
            _ => throw new Xunit.Sdk.XunitException("Não deveria iniciar."));

        var excecao = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            preflight.PrepararAsync(CancellationToken.None));

        Assert.Contains("OBS Studio", excecao.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeveExplicarTimeoutDoWebsocket()
    {
        var executavel = Path.GetTempFileName();
        try
        {
            var preflight = CriarPreflight(
                executavel,
                (_, _) => Task.FromResult(false),
                _ => { },
                maximoTentativas: 2);

            var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                preflight.PrepararAsync(CancellationToken.None));

            Assert.Contains("websocket", excecao.Message, StringComparison.OrdinalIgnoreCase);
            // A mensagem reflete os parâmetros reais da espera, e não um texto fixo.
            Assert.Contains("2 tentativas", excecao.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(executavel);
        }
    }

    private static ObsProcessPreflight CriarPreflight(
        string caminhoExecutavel,
        Func<Uri, CancellationToken, Task<bool>> verificarDisponibilidade,
        Action<ProcessStartInfo> iniciarProcesso,
        int maximoTentativas = 1) =>
        new(
            new Uri("ws://127.0.0.1:4455"),
            caminhoExecutavel,
            verificarDisponibilidade,
            iniciarProcesso,
            maximoTentativas,
            TimeSpan.Zero);
}
