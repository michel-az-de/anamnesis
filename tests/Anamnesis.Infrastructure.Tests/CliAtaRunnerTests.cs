using System.Text;
using System.Text.Json;
using Anamnesis.Application.Modelos;
using Anamnesis.Domain.Entidades;
using Anamnesis.Infrastructure.Cli;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class CliAtaRunnerTests
{
    [Fact]
    public async Task DevePreservarCaracteresUtf8DaCliFake()
    {
        const string script = "[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false; " +
                              "[Console]::In.ReadToEnd() | Out-Null; " +
                              "[Console]::Out.Write('{\"resumoExecutivo\":\"Reunião válida.\",\"decisoes\":[\"Aprovação concluída.\"],\"tarefas\":[{\"descricao\":\"Revisão técnica.\",\"responsavel\":\"João\",\"prazo\":null}]}')";
        var comandoCodificado = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var runner = new CliAtaRunner(new CliAtaRunnerOptions(
            "PowerShell fake",
            Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
            ["-NoProfile", "-EncodedCommand", comandoCodificado]));
        var reuniao = new Reuniao(Guid.NewGuid(), "Reunião", DateTimeOffset.UtcNow);

        var ata = await runner.GerarAsync(
            reuniao,
            new TranscricaoGerada("Transcrição em português.", "pt"),
            CancellationToken.None);

        Assert.Equal("Reunião válida.", ata.ResumoExecutivo);
        Assert.Equal("Aprovação concluída.", Assert.Single(ata.Decisoes));
        Assert.Equal("João", Assert.Single(ata.Tarefas).Responsavel);
    }

    [Fact]
    public async Task DeveConcluirQuandoACliEscreveAntesDeDrenarAEntrada()
    {
        // A CLI enche o buffer do pipe de erro antes de ler a entrada. Se o runner só drenar
        // a saída depois de escrever a transcrição inteira, os dois processos travam entre si.
        var runner = CriarRunner(
            "[Console]::Error.Write('d' * 200000); " +
            "[Console]::In.ReadToEnd() | Out-Null; " +
            "[Console]::Out.Write('{\"resumoExecutivo\":\"Resumo longo.\",\"decisoes\":[],\"tarefas\":[]}')");
        var reuniao = new Reuniao(Guid.NewGuid(), "Reunião longa", DateTimeOffset.UtcNow);
        var transcricao = new TranscricaoGerada(new string('t', 1_000_000), "pt");

        var geracao = runner.GerarAsync(reuniao, transcricao, CancellationToken.None);
        var concluiu = await Task.WhenAny(geracao, Task.Delay(TimeSpan.FromSeconds(30))) == geracao;

        Assert.True(concluiu, "A CLI travou: a saída não foi drenada enquanto a transcrição era escrita.");
        Assert.Equal("Resumo longo.", (await geracao).ResumoExecutivo);
    }

    [Fact]
    public async Task DeveIncluirSaidaDeErroNaFalhaDaCli()
    {
        var runner = CriarRunner(
            "[Console]::In.ReadToEnd() | Out-Null; " +
            "[Console]::Error.Write('credencial expirada'); " +
            "exit 3");
        var reuniao = new Reuniao(Guid.NewGuid(), "Reunião", DateTimeOffset.UtcNow);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.GerarAsync(
            reuniao,
            new TranscricaoGerada("Transcrição.", "pt"),
            CancellationToken.None));

        Assert.Contains("credencial expirada", excecao.Message, StringComparison.Ordinal);
        Assert.Contains("3", excecao.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeveLerMensagemFinalDeArquivoQuandoStdoutContemProgresso()
    {
        var diretorio = Path.Combine(Path.GetTempPath(), $"anamnesis-cli-final-{Guid.NewGuid():N}");
        Directory.CreateDirectory(diretorio);
        try
        {
            var script = Path.Combine(diretorio, "cli-final.ps1");
            await File.WriteAllTextAsync(
                script,
                "param([string]$SaidaFinal)\n" +
                "$null = [Console]::In.ReadToEnd()\n" +
                "[Console]::Out.Write('progresso que nao e json')\n" +
                "[IO.File]::WriteAllText($SaidaFinal, '{\"resumoExecutivo\":\"Ata recuperada.\",\"decisoes\":[],\"tarefas\":[]}', [Text.UTF8Encoding]::new($false))\n");
            var runner = new CliAtaRunner(new CliAtaRunnerOptions(
                "PowerShell com arquivo final",
                Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script],
                ArgumentoArquivoSaida: "-SaidaFinal"));
            var reuniao = new Reuniao(Guid.NewGuid(), "Reunião", DateTimeOffset.UtcNow);

            var ata = await runner.GerarAsync(
                reuniao,
                new TranscricaoGerada("Transcrição.", "pt"),
                CancellationToken.None);

            Assert.Equal("Ata recuperada.", ata.ResumoExecutivo);
            Assert.Empty(Directory.GetFiles(diretorio, "*.json"));
        }
        finally
        {
            Directory.Delete(diretorio, recursive: true);
        }
    }

    [Fact]
    public async Task DeveTentarNovamenteQuandoPrimeiraRespostaNaoForJson()
    {
        var marcador = Path.Combine(Path.GetTempPath(), $"anamnesis-cli-retry-{Guid.NewGuid():N}.tmp");
        try
        {
            var caminhoEscapado = marcador.Replace("'", "''", StringComparison.Ordinal);
            var runner = CriarRunner(
                $"if (Test-Path -LiteralPath '{caminhoEscapado}') {{ " +
                "[Console]::Out.Write('{\"resumoExecutivo\":\"Ata na segunda tentativa.\",\"decisoes\":[],\"tarefas\":[]}') " +
                $"}} else {{ [IO.File]::WriteAllText('{caminhoEscapado}', '1'); [Console]::Out.Write('resposta invalida') }}");
            var reuniao = new Reuniao(Guid.NewGuid(), "Reunião", DateTimeOffset.UtcNow);

            var ata = await runner.GerarAsync(
                reuniao,
                new TranscricaoGerada("Transcrição.", "pt"),
                CancellationToken.None);

            Assert.Equal("Ata na segunda tentativa.", ata.ResumoExecutivo);
            Assert.True(File.Exists(marcador));
        }
        finally
        {
            File.Delete(marcador);
        }
    }

    [Fact]
    public async Task DeveSolicitarAtaNarrativaComContextoVerificavelSemInventarParticipantes()
    {
        var entradaCapturada = Path.Combine(Path.GetTempPath(), $"anamnesis-entrada-narrativa-{Guid.NewGuid():N}.json");
        try
        {
            var caminhoEscapado = entradaCapturada.Replace("'", "''", StringComparison.Ordinal);
            var runner = CriarRunner(
                "$entrada = [Console]::In.ReadToEnd(); " +
                $"[IO.File]::WriteAllText('{caminhoEscapado}', $entrada, [Text.UTF8Encoding]::new($false)); " +
                "[Console]::Out.Write('{\"resumoExecutivo\":\"Relato narrativo.\",\"decisoes\":[],\"tarefas\":[]}')");
            var inicio = new DateTimeOffset(2026, 8, 7, 9, 0, 0, TimeSpan.FromHours(-3));
            var reuniao = new Reuniao(Guid.NewGuid(), "Planejamento comercial", inicio);
            reuniao.IniciarGravacao(inicio);
            reuniao.FinalizarGravacao("gravacao.mp4", inicio.AddMinutes(90));

            await runner.GerarAsync(
                reuniao,
                new TranscricaoGerada("Pessoa 1: Vamos definir o MVP.", "pt"),
                CancellationToken.None);

            using var entrada = JsonDocument.Parse(await File.ReadAllTextAsync(entradaCapturada));
            var contexto = entrada.RootElement.GetProperty("reuniao");
            var instrucao = entrada.RootElement.GetProperty("instrucao").GetString()!;
            Assert.Equal("Planejamento comercial", contexto.GetProperty("titulo").GetString());
            Assert.Equal(5400, contexto.GetProperty("duracaoSegundos").GetInt32());
            Assert.Contains("terceira pessoa", instrucao, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ordem cronológica", instrucao, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("não invente", instrucao, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("um a quatro parágrafos", instrucao, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(entradaCapturada);
        }
    }

    private static CliAtaRunner CriarRunner(string script) => new(new CliAtaRunnerOptions(
        "PowerShell fake",
        Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
        [
            "-NoProfile",
            "-EncodedCommand",
            Convert.ToBase64String(Encoding.Unicode.GetBytes(
                "[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false; " + script))
        ]));
}
