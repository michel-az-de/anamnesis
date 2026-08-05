using System.Diagnostics;
using Anamnesis.Infrastructure.Processos;

namespace Anamnesis.Infrastructure.Whisper;

public sealed class AudioPreparadorFfmpeg
{
    private static readonly TimeSpan TimeoutPadrao = TimeSpan.FromMinutes(10);
    private readonly string _caminhoExecutavel;
    private readonly TimeSpan _timeout;

    public AudioPreparadorFfmpeg(string caminhoExecutavel, TimeSpan? timeout = null)
    {
        _caminhoExecutavel = caminhoExecutavel;
        _timeout = timeout ?? TimeoutPadrao;
    }

    public async Task<string> PrepararAsync(string caminhoGravacao, CancellationToken cancellationToken)
    {
        if (!File.Exists(_caminhoExecutavel))
        {
            throw new FileNotFoundException("O executável do FFmpeg não foi encontrado.", _caminhoExecutavel);
        }

        if (!File.Exists(caminhoGravacao))
        {
            throw new FileNotFoundException("A gravação não foi encontrada.", caminhoGravacao);
        }

        var diretorioTemporario = Path.Combine(Path.GetTempPath(), "anamnesis", "audio");
        Directory.CreateDirectory(diretorioTemporario);
        var caminhoAudio = Path.Combine(diretorioTemporario, $"{Guid.NewGuid():N}.wav");
        var inicio = new ProcessStartInfo(_caminhoExecutavel)
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
            using var deadline = ProcessoExterno.CriarDeadline(_timeout, cancellationToken);
            var erroPendente = processo.StandardError.ReadToEndAsync(deadline.Token);
            try
            {
                await processo.WaitForExitAsync(deadline.Token);
                var erro = await erroPendente;
                if (processo.ExitCode != 0)
                {
                    throw new InvalidOperationException($"FFmpeg falhou com código {processo.ExitCode}: {erro.Trim()}");
                }
            }
            catch (OperationCanceledException excecao)
            {
                await ProcessoExterno.EncerrarAsync(processo);
                await ProcessoExterno.ObservarSemSubstituirErroAsync(erroPendente);
                cancellationToken.ThrowIfCancellationRequested();
                throw ProcessoExterno.CriarTimeout("FFmpeg", _timeout, excecao);
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
