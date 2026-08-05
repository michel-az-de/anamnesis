using System.Diagnostics;
using System.Globalization;
using System.Text;
using Anamnesis.Application.Modelos;
using Anamnesis.Domain.Entidades;
using Anamnesis.Infrastructure.Cli;
using Anamnesis.Infrastructure.Whisper;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

[Collection(ProcessosExternosGrupo.Nome)]
public sealed class ProcessosExternosDeadlineTests
{
    private static readonly TimeSpan DeadlineProcesso = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DeadlineLogico = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan GuardaDoTeste = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task CliAtaDeveEncerrarProcessoTravadoMesmoSemTokenDoChamador()
    {
        var diretorio = CriarDiretorioTemporario();
        try
        {
            var caminhoPid = Path.Combine(diretorio, "cli.pid");
            var runner = new CliAtaRunner(new CliAtaRunnerOptions(
                "CLI travada",
                CaminhoPowerShell(),
                ArgumentosPowerShellTravado(caminhoPid),
                DeadlineProcesso));
            var reuniao = new Reuniao(Guid.NewGuid(), "Reunião", DateTimeOffset.UtcNow);

            var operacao = runner.GerarAsync(
                reuniao,
                new TranscricaoGerada("Transcrição", "pt"),
                CancellationToken.None);

            var excecao = await AguardarTimeoutAsync(operacao, caminhoPid);

            Assert.Contains("CLI travada", excecao.Message, StringComparison.Ordinal);
            await AssertProcessoEncerradoAsync(caminhoPid);
        }
        finally
        {
            Directory.Delete(diretorio, recursive: true);
        }
    }

    [Fact]
    public async Task FfmpegDeveEncerrarProcessoTravadoMesmoSemTokenDoChamador()
    {
        var diretorio = CriarDiretorioTemporario();
        try
        {
            var caminhoPid = Path.Combine(diretorio, "ffmpeg.pid");
            var ffmpeg = await CriarCmdPowerShellTravadoAsync(diretorio, "ffmpeg.cmd", caminhoPid);
            var gravacao = Path.Combine(diretorio, "gravacao.mkv");
            await File.WriteAllTextAsync(gravacao, "áudio");
            var preparador = new AudioPreparadorFfmpeg(ffmpeg, DeadlineProcesso);

            var operacao = preparador.PrepararAsync(gravacao, CancellationToken.None);

            var excecao = await AguardarTimeoutAsync(operacao, caminhoPid);

            Assert.Contains("FFmpeg", excecao.Message, StringComparison.Ordinal);
            await AssertProcessoEncerradoAsync(caminhoPid);
        }
        finally
        {
            Directory.Delete(diretorio, recursive: true);
        }
    }

    [Fact]
    public async Task WhisperDeveEncerrarProcessoTravadoMesmoSemTokenDoChamador()
    {
        var diretorio = CriarDiretorioTemporario();
        try
        {
            var caminhoPid = Path.Combine(diretorio, "whisper.pid");
            var ffmpeg = await CriarFfmpegFakeAsync(diretorio);
            var whisper = await CriarCmdPowerShellTravadoAsync(diretorio, "whisper.cmd", caminhoPid);
            var modelo = Path.Combine(diretorio, "modelo.bin");
            var gravacao = Path.Combine(diretorio, "gravacao.mkv");
            await File.WriteAllTextAsync(modelo, "modelo");
            await File.WriteAllTextAsync(gravacao, "áudio");
            var transcritor = new WhisperTranscritor(new WhisperOptions(
                whisper,
                modelo,
                "pt",
                ffmpeg,
                TimeoutWhisper: DeadlineProcesso,
                TimeoutFfmpeg: TimeSpan.FromSeconds(2)));

            var operacao = transcritor.TranscreverAsync(gravacao, CancellationToken.None);

            var excecao = await AguardarTimeoutAsync(operacao, caminhoPid);

            Assert.Contains("Whisper", excecao.Message, StringComparison.Ordinal);
            await AssertProcessoEncerradoAsync(caminhoPid);
        }
        finally
        {
            Directory.Delete(diretorio, recursive: true);
        }
    }

    [Fact]
    public async Task DockerPreflightDeveExpirarVerificacaoTravadaMesmoSemTokenDoChamador()
    {
        var verificacaoQueNuncaTermina = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var preflight = new DockerProcessPreflight(
            "C:\\docker\\docker.exe",
            "C:\\docker\\Docker Desktop.exe",
            _ => verificacaoQueNuncaTermina.Task,
            _ => { },
            maximoTentativas: 1,
            intervalo: TimeSpan.Zero,
            timeoutVerificacao: DeadlineLogico);

        var operacao = preflight.PrepararAsync(CancellationToken.None);
        var concluida = await Task.WhenAny(operacao, Task.Delay(GuardaDoTeste));

        Assert.Same(operacao, concluida);
        var excecao = await Assert.ThrowsAsync<TimeoutException>(() => operacao);
        Assert.Contains("Docker", excecao.Message, StringComparison.Ordinal);
    }

    private static async Task<TimeoutException> AguardarTimeoutAsync(Task operacao, string caminhoPid)
    {
        var concluida = await Task.WhenAny(operacao, Task.Delay(GuardaDoTeste));
        if (concluida != operacao)
        {
            EncerrarProcessoPeloPid(caminhoPid);
        }

        Assert.Same(operacao, concluida);
        return await Assert.ThrowsAsync<TimeoutException>(() => operacao);
    }

    private static async Task AssertProcessoEncerradoAsync(string caminhoPid)
    {
        var limite = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!File.Exists(caminhoPid) && DateTimeOffset.UtcNow < limite)
        {
            await Task.Delay(20);
        }

        Assert.True(File.Exists(caminhoPid), "O processo fake não registrou seu PID.");
        var pid = int.Parse(await File.ReadAllTextAsync(caminhoPid), CultureInfo.InvariantCulture);
        limite = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < limite)
        {
            try
            {
                using var processo = Process.GetProcessById(pid);
                if (processo.HasExited)
                {
                    return;
                }
            }
            catch (ArgumentException)
            {
                return;
            }

            await Task.Delay(20);
        }

        EncerrarProcessoPeloPid(caminhoPid);
        Assert.Fail($"O processo fake {pid} permaneceu ativo após o deadline.");
    }

    private static void EncerrarProcessoPeloPid(string caminhoPid)
    {
        if (!File.Exists(caminhoPid) || !int.TryParse(File.ReadAllText(caminhoPid), out var pid))
        {
            return;
        }

        try
        {
            using var processo = Process.GetProcessById(pid);
            processo.Kill(entireProcessTree: true);
        }
        catch (ArgumentException)
        {
        }
    }

    private static string[] ArgumentosPowerShellTravado(string caminhoPid)
    {
        var caminhoEscapado = caminhoPid.Replace("'", "''", StringComparison.Ordinal);
        var script = $"$PID | Set-Content -LiteralPath '{caminhoEscapado}' -NoNewline; Start-Sleep -Seconds 30";
        return
        [
            "-NoProfile",
            "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes(script))
        ];
    }

    private static async Task<string> CriarCmdPowerShellTravadoAsync(
        string diretorio,
        string nome,
        string caminhoPid)
    {
        var caminho = Path.Combine(diretorio, nome);
        var argumentos = ArgumentosPowerShellTravado(caminhoPid);
        await File.WriteAllTextAsync(
            caminho,
            $"@echo off\r\n\"{CaminhoPowerShell()}\" {string.Join(' ', argumentos)}\r\n");
        return caminho;
    }

    private static async Task<string> CriarFfmpegFakeAsync(string diretorio)
    {
        var caminho = Path.Combine(diretorio, "ffmpeg-ok.cmd");
        await File.WriteAllTextAsync(
            caminho,
            "@echo off\r\nset \"saida=\"\r\n:argumento\r\nif \"%~1\"==\"\" goto fim\r\nset \"saida=%~1\"\r\nshift\r\ngoto argumento\r\n:fim\r\necho WAV convertido> \"%saida%\"\r\n");
        return caminho;
    }

    private static string CriarDiretorioTemporario()
    {
        var caminho = Path.Combine(Path.GetTempPath(), $"anamnesis-deadline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(caminho);
        return caminho;
    }

    private static string CaminhoPowerShell() => Path.Combine(
        Environment.SystemDirectory,
        "WindowsPowerShell",
        "v1.0",
        "powershell.exe");
}

[CollectionDefinition(Nome, DisableParallelization = true)]
public sealed class ProcessosExternosGrupo
{
    public const string Nome = "Processos externos com deadline";
}
