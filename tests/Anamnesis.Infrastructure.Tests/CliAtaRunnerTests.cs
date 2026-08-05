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
}
