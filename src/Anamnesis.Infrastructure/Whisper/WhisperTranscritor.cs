using System.Diagnostics;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;

namespace Anamnesis.Infrastructure.Whisper;

public sealed class WhisperTranscritor : ITranscritor
{
    private readonly WhisperOptions _options;
    private readonly IDockerPreflight? _dockerPreflight;

    public WhisperTranscritor(WhisperOptions options, IDockerPreflight? dockerPreflight = null)
    {
        _options = options;
        _dockerPreflight = dockerPreflight;
    }

    public async Task<TranscricaoGerada> TranscreverAsync(string caminhoArquivo, CancellationToken cancellationToken)
    {
        var options = _options;
        if (!File.Exists(options.CaminhoExecutavel))
        {
            throw new FileNotFoundException("O executável do Whisper não foi encontrado.", options.CaminhoExecutavel);
        }

        if (!File.Exists(options.CaminhoModelo))
        {
            throw new FileNotFoundException("O modelo do Whisper não foi encontrado.", options.CaminhoModelo);
        }

        if (!string.IsNullOrWhiteSpace(options.ImagemDocker) && _dockerPreflight is not null)
        {
            await _dockerPreflight.PrepararAsync(cancellationToken);
        }

        var caminhoAudio = await new AudioPreparadorFfmpeg(options.CaminhoExecutavelFfmpeg)
            .PrepararAsync(caminhoArquivo, cancellationToken);
        var diretorioTemporario = Path.Combine(Path.GetTempPath(), "anamnesis", "whisper");
        Directory.CreateDirectory(diretorioTemporario);
        var caminhoSaida = Path.Combine(diretorioTemporario, Guid.NewGuid().ToString("N"));
        var caminhoTexto = $"{caminhoSaida}.txt";
        try
        {
            var inicio = new ProcessStartInfo(options.CaminhoExecutavel)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var argumentos = string.IsNullOrWhiteSpace(options.ImagemDocker)
                ? WhisperComando.Criar(options, caminhoAudio, caminhoSaida)
                : WhisperDockerComando.Criar(options, caminhoAudio, caminhoSaida);
            foreach (var argumento in argumentos)
            {
                inicio.ArgumentList.Add(argumento);
            }

            using var processo = Process.Start(inicio)
                ?? throw new InvalidOperationException("Não foi possível iniciar o Whisper local.");
            var erro = await processo.StandardError.ReadToEndAsync(cancellationToken);
            await processo.WaitForExitAsync(cancellationToken);
            if (processo.ExitCode != 0)
            {
                throw new InvalidOperationException($"Whisper falhou com código {processo.ExitCode}: {erro.Trim()}");
            }

            var texto = await File.ReadAllTextAsync(caminhoTexto, cancellationToken);
            if (string.IsNullOrWhiteSpace(texto))
            {
                throw new InvalidOperationException("Whisper gerou uma transcrição vazia.");
            }

            return new TranscricaoGerada(texto.Trim(), options.Idioma);
        }
        finally
        {
            File.Delete(caminhoAudio);
            File.Delete(caminhoTexto);
        }
    }
}
