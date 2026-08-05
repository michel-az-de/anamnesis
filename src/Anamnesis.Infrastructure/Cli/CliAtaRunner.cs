using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Domain.Entidades;
using Anamnesis.Infrastructure.Processos;

namespace Anamnesis.Infrastructure.Cli;

public sealed class CliAtaRunner(CliAtaRunnerOptions options) : IAtaRunner
{
    private static readonly Encoding Utf8SemBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly TimeSpan TimeoutPadrao = TimeSpan.FromMinutes(10);

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
        var timeout = options.Timeout ?? TimeoutPadrao;
        using var deadline = ProcessoExterno.CriarDeadline(timeout, cancellationToken);

        // Drenar saída e erro antes de escrever a entrada: uma transcrição grande não cabe no
        // buffer do pipe, e uma CLI que emite algo antes de consumir tudo travaria com o runner.
        var saidaPendente = processo.StandardOutput.ReadToEndAsync(deadline.Token);
        var erroPendente = processo.StandardError.ReadToEndAsync(deadline.Token);

        var entrada = JsonSerializer.Serialize(new
        {
            reuniao = new { reuniao.Id, reuniao.Titulo, reuniao.CriadaEm },
            transcricao = new { transcricao.Texto, transcricao.Idioma },
            instrucao = "Retorne somente JSON com resumoExecutivo, decisoes e tarefas."
        });
        try
        {
            await processo.StandardInput.WriteAsync(entrada.AsMemory(), deadline.Token);
            processo.StandardInput.Close();

            await processo.WaitForExitAsync(deadline.Token);
            var saida = await saidaPendente;
            var erro = (await erroPendente).Trim();
            if (processo.ExitCode != 0)
            {
                throw new InvalidOperationException(erro.Length == 0
                    ? $"A CLI '{Nome}' falhou com código {processo.ExitCode}."
                    : $"A CLI '{Nome}' falhou com código {processo.ExitCode}: {erro}");
            }

            return AtaEstruturadaJson.Converter(saida);
        }
        catch (OperationCanceledException excecao)
        {
            await ProcessoExterno.EncerrarAsync(processo);
            await ProcessoExterno.ObservarSemSubstituirErroAsync(saidaPendente, erroPendente);
            cancellationToken.ThrowIfCancellationRequested();
            throw ProcessoExterno.CriarTimeout($"A CLI '{Nome}'", timeout, excecao);
        }
    }
}
