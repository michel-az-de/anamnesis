namespace Anamnesis.Infrastructure.Whisper;

public static class FfmpegComando
{
    public static IReadOnlyList<string> Criar(string caminhoEntrada, string caminhoSaida) =>
        ["-y", "-i", caminhoEntrada, "-vn", "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", caminhoSaida];
}
