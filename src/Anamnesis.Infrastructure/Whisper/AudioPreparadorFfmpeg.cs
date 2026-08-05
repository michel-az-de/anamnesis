using System.Diagnostics;

namespace Anamnesis.Infrastructure.Whisper;

public sealed class AudioPreparadorFfmpeg(string caminhoExecutavel)
{
    public async Task<string> PrepararAsync(string caminhoGravacao, CancellationToken cancellationToken)
    {
        if (!File.Exists(caminhoExecutavel))
        {
            throw new FileNotFoundException("O executável do FFmpeg não foi encontrado.", caminhoExecutavel);
        }

        if (!File.Exists(caminhoGravacao))
        {
            throw new FileNotFoundException("A gravação não foi encontrada.", caminhoGravacao);
        }

        var diretorioTemporario = Path.Combine(Path.GetTempPath(), "anamnesis", "audio");
        Directory.CreateDirectory(diretorioTemporario);
        var caminhoAudio = Path.Combine(diretorioTemporario, $"{Guid.NewGuid():N}.wav");
        var inicio = new ProcessStartInfo(caminhoExecutavel)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argumento in FfmpegComando.Criar(caminhoGravacao, caminhoAudio))
        {
            inicio.ArgumentList.Add(argumento);
        }

        try
        {
            using var processo = Process.Start(inicio)
                ?? throw new InvalidOperationException("Não foi possível iniciar o FFmpeg local.");
            var erro = await processo.StandardError.ReadToEndAsync(cancellationToken);
            await processo.WaitForExitAsync(cancellationToken);
            if (processo.ExitCode != 0)
            {
                throw new InvalidOperationException($"FFmpeg falhou com código {processo.ExitCode}: {erro.Trim()}");
            }

            if (!File.Exists(caminhoAudio))
            {
                throw new InvalidOperationException("FFmpeg não produziu o áudio WAV esperado.");
            }

            return caminhoAudio;
        }
        catch
        {
            ArquivoTemporario.ExcluirSilenciosamente(caminhoAudio);
            throw;
        }
    }
}
