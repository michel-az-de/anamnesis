using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Domain.Entidades;

namespace Anamnesis.Infrastructure.Cli;

public sealed class CliAtaRunner(CliAtaRunnerOptions options) : IAtaRunner
{
    private static readonly Encoding Utf8SemBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public string Nome => options.Nome;

    public async Task<AtaGerada> GerarAsync(Reuniao reuniao, TranscricaoGerada transcricao, CancellationToken cancellationToken)
    {
        var inicio = new ProcessStartInfo(options.CaminhoExecutavel)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Utf8SemBom,
            StandardOutputEncoding = Utf8SemBom,
            StandardErrorEncoding = Utf8SemBom,
            CreateNoWindow = true
        };

        foreach (var argumento in options.Argumentos)
        {
            inicio.ArgumentList.Add(argumento);
        }

        using var processo = Process.Start(inicio)
            ?? throw new InvalidOperationException($"Não foi possível iniciar a CLI '{Nome}'.");
        var entrada = JsonSerializer.Serialize(new
        {
            reuniao = new { reuniao.Id, reuniao.Titulo, reuniao.CriadaEm },
            transcricao = new { transcricao.Texto, transcricao.Idioma },
            instrucao = "Retorne somente JSON com resumoExecutivo, decisoes e tarefas."
        });
        await processo.StandardInput.WriteAsync(entrada.AsMemory(), cancellationToken);
        processo.StandardInput.Close();

        var saidaPendente = processo.StandardOutput.ReadToEndAsync(cancellationToken);
        var erroPendente = processo.StandardError.ReadToEndAsync(cancellationToken);
        await processo.WaitForExitAsync(cancellationToken);
        var saida = await saidaPendente;
        await erroPendente;
        if (processo.ExitCode != 0)
        {
            throw new InvalidOperationException($"A CLI '{Nome}' falhou com código {processo.ExitCode}.");
        }

        return AtaEstruturadaJson.Converter(saida);
    }
}
