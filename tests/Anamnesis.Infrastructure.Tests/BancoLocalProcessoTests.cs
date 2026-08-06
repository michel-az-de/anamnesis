using System.Diagnostics;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class BancoLocalProcessoTests : IAsyncLifetime
{
    private static readonly TimeSpan LimiteProcesso = TimeSpan.FromSeconds(10);
    private readonly string _diretorio = Path.Combine(
        Path.GetTempPath(),
        $"anamnesis-banco-processo-{Guid.NewGuid():N}");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_diretorio);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_diretorio))
        {
            Directory.Delete(_diretorio, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task DoisProcessosDevemTransferirExclusividadeDoMesmoArquivo()
    {
        var caminhoProbe = ResolverCaminhoProbe();
        var caminhoBanco = Path.Combine(_diretorio, "journal.db");
        var sufixo = Guid.NewGuid().ToString("N");
        var nomeDonoAdquiriu = $@"Local\Anamnesis.Tests.JournalProbe.{sufixo}.dono";
        var nomeLiberarDono = $@"Local\Anamnesis.Tests.JournalProbe.{sufixo}.liberar";
        var nomeConcorrenteDetectou = $@"Local\Anamnesis.Tests.JournalProbe.{sufixo}.concorrente";
        using var donoAdquiriu = new EventWaitHandle(
            false,
            EventResetMode.ManualReset,
            nomeDonoAdquiriu);
        using var liberarDono = new EventWaitHandle(
            false,
            EventResetMode.ManualReset,
            nomeLiberarDono);
        using var concorrenteDetectou = new EventWaitHandle(
            false,
            EventResetMode.ManualReset,
            nomeConcorrenteDetectou);
        using var dono = IniciarProbe(
            caminhoProbe,
            "dono",
            caminhoBanco,
            nomeDonoAdquiriu,
            nomeLiberarDono);

        ProcessoProbe? concorrente = null;
        try
        {
            Assert.True(donoAdquiriu.WaitOne(LimiteProcesso));
            concorrente = IniciarProbe(
                caminhoProbe,
                "concorrente",
                caminhoBanco,
                nomeConcorrenteDetectou);
            Assert.True(concorrenteDetectou.WaitOne(LimiteProcesso));
            liberarDono.Set();
            var resultados = await Task.WhenAll(
                dono.AguardarAsync(LimiteProcesso),
                concorrente.AguardarAsync(LimiteProcesso));
            Assert.All(resultados, resultado => Assert.True(
                resultado.CodigoSaida == 0,
                $"Probe encerrou com {resultado.CodigoSaida}. stdout={resultado.Stdout} stderr={resultado.Stderr}"));
        }
        finally
        {
            liberarDono.Set();
            concorrente?.Dispose();
        }
    }

    private static string ResolverCaminhoProbe()
    {
        var configuracao = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var caminho = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "Anamnesis.JournalProbe",
            "bin",
            configuracao,
            "net10.0-windows",
            "Anamnesis.JournalProbe.dll"));
        Assert.True(File.Exists(caminho), $"Probe não encontrado: {caminho}");
        return caminho;
    }

    private static ProcessoProbe IniciarProbe(
        string caminhoProbe,
        params string[] argumentos)
    {
        var processo = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        processo.StartInfo.ArgumentList.Add(caminhoProbe);
        foreach (var argumento in argumentos)
        {
            processo.StartInfo.ArgumentList.Add(argumento);
        }

        Assert.True(processo.Start());
        return new ProcessoProbe(
            processo,
            processo.StandardOutput.ReadToEndAsync(),
            processo.StandardError.ReadToEndAsync());
    }

    private sealed class ProcessoProbe(
        Process processo,
        Task<string> stdout,
        Task<string> stderr) : IDisposable
    {
        public async Task<ResultadoProcesso> AguardarAsync(TimeSpan limite)
        {
            await processo.WaitForExitAsync().WaitAsync(limite);
            return new ResultadoProcesso(
                processo.ExitCode,
                await stdout,
                await stderr);
        }

        public void Dispose()
        {
            if (!processo.HasExited)
            {
                processo.Kill(entireProcessTree: true);
                processo.WaitForExit(milliseconds: 5000);
            }

            processo.Dispose();
        }
    }

    private sealed record ResultadoProcesso(
        int CodigoSaida,
        string Stdout,
        string Stderr);
}
