namespace Anamnesis.Infrastructure.Whisper;

public static class WhisperComando
{
    public static IReadOnlyList<string> Criar(WhisperOptions options, string caminhoAudio, string caminhoSaida) =>
        ["-m", options.CaminhoModelo, "-f", caminhoAudio, "-l", options.Idioma, "-otxt", "-of", caminhoSaida];
}
