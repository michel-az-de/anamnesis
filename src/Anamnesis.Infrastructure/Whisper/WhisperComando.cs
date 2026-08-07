namespace Anamnesis.Infrastructure.Whisper;

public static class WhisperComando
{
    public static IReadOnlyList<string> Criar(WhisperOptions options, string caminhoAudio, string caminhoSaida) =>
        ["-m", options.CaminhoModelo, "-f", caminhoAudio, "-l", options.Idioma, "-mc", "0", "-sns", "-np", "-otxt", "-of", caminhoSaida];
}
