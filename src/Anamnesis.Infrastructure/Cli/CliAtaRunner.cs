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
        try
        {
            return await ExecutarUmaVezAsync(reuniao, transcricao, cancellationToken);
        }
        catch (AtaJsonInvalidoException)
        {
            return await ExecutarUmaVezAsync(reuniao, transcricao, cancellationToken);
        }
    }

    private async Task<AtaGerada> ExecutarUmaVezAsync(
        Reuniao reuniao,
        TranscricaoGerada transcricao,
        CancellationToken cancellationToken)
    {
        var caminhoMensagemFinal = CriarCaminhoMensagemFinal();
        try
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

            if (caminhoMensagemFinal is not null)
            {
                inicio.ArgumentList.Add(options.ArgumentoArquivoSaida!);
                inicio.ArgumentList.Add(caminhoMensagemFinal);
            }

            using var processo = Process.Start(inicio)
                ?? throw new InvalidOperationException($"Não foi possível iniciar a CLI '{Nome}'.");
            var timeout = options.Timeout ?? TimeoutPadrao;
            using var deadline = ProcessoExterno.CriarDeadline(timeout, cancellationToken);

            // Drenar saída e erro antes de escrever a entrada: uma transcrição grande não cabe no
            // buffer do pipe, e uma CLI que emite algo antes de consumir tudo travaria com o runner.
            var saidaPendente = processo.StandardOutput.ReadToEndAsync(deadline.Token);
            var erroPendente = processo.StandardError.ReadToEndAsync(deadline.Token);

            var duracaoSegundos = reuniao.Gravacao is null
                ? (long?)null
                : Math.Max(
                    0,
                    (long)(reuniao.Gravacao.FinalizadaEm - reuniao.Gravacao.IniciadaEm).TotalSeconds);
            var entrada = JsonSerializer.Serialize(new
            {
                reuniao = new
                {
                    id = reuniao.Id,
                    titulo = reuniao.Titulo,
                    criadaEm = reuniao.CriadaEm,
                    gravacaoIniciadaEm = reuniao.Gravacao?.IniciadaEm,
                    gravacaoFinalizadaEm = reuniao.Gravacao?.FinalizadaEm,
                    duracaoSegundos
                },
                transcricao = new { texto = transcricao.Texto, idioma = transcricao.Idioma },
                instrucao =
                    "Retorne somente JSON com resumoExecutivo, decisoes e tarefas. " +
                    "O resumoExecutivo deve ser um relato factual em terceira pessoa, em ordem cronológica, " +
                    "com um a quatro parágrafos curtos. Contextualize título, data e duração; descreva os assuntos, " +
                    "o desenvolvimento da conversa, como ela terminou e o que ficou definido. Informe a quantidade " +
                    "estimada de locutores somente quando ela puder ser derivada de rótulos presentes na transcrição. " +
                    "Não invente participantes, nomes, presenças, falas, consenso ou fatos ausentes. " +
                    "Locutores sem identidade confirmada devem permanecer como Pessoa 1, Pessoa 2 e assim por diante. " +
                    "Em tarefas, prazo deve ser uma data no formato yyyy-MM-dd ou null; " +
                    "use a data de criação da reunião para resolver referências relativas."
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

                var respostaFinal = caminhoMensagemFinal is null
                    ? saida
                    : await File.ReadAllTextAsync(caminhoMensagemFinal, deadline.Token);
                return AtaEstruturadaJson.Converter(respostaFinal);
            }
            catch (OperationCanceledException excecao)
            {
                await ProcessoExterno.EncerrarAsync(processo);
                await ProcessoExterno.ObservarSemSubstituirErroAsync(saidaPendente, erroPendente);
                cancellationToken.ThrowIfCancellationRequested();
                throw ProcessoExterno.CriarTimeout($"A CLI '{Nome}'", timeout, excecao);
            }
        }
        finally
        {
            ExcluirSilenciosamente(caminhoMensagemFinal);
        }
    }

    private string? CriarCaminhoMensagemFinal()
    {
        if (string.IsNullOrWhiteSpace(options.ArgumentoArquivoSaida))
        {
            return null;
        }

        var diretorio = Path.Combine(Path.GetTempPath(), "anamnesis", "cli");
        Directory.CreateDirectory(diretorio);
        return Path.Combine(diretorio, $"{Guid.NewGuid():N}.json");
    }

    private static void ExcluirSilenciosamente(string? caminho)
    {
        if (caminho is null)
        {
            return;
        }

        try
        {
            File.Delete(caminho);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
