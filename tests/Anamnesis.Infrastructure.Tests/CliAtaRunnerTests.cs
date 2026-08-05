using System.Text;
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
