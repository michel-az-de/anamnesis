namespace Anamnesis.Infrastructure.Whisper;

public static class WhisperDockerComando
{
    public static IReadOnlyList<string> Criar(
        WhisperOptions options,
        string caminhoAudio,
        string caminhoSaida)
    {
        if (string.IsNullOrWhiteSpace(options.ImagemDocker))
        {
            throw new ArgumentException("A imagem Docker do Whisper não foi configurada.");
        }

        if (options.Idioma.Any(caractere => !char.IsLetter(caractere) && caractere != '-'))
        {
            throw new ArgumentException("O idioma do Whisper contém caracteres inválidos.");
        }

        var diretorioSaida = Path.GetDirectoryName(caminhoSaida)
            ?? throw new ArgumentException("O caminho de saída do Whisper não possui diretório.");
        var nomeSaida = Path.GetFileName(caminhoSaida);
        return
        [
            "run", "--rm",
            "-v", $"{options.CaminhoModelo}:/models/model.bin:ro",
            "-v", $"{caminhoAudio}:/audio/input.wav:ro",
            "-v", $"{diretorioSaida}:/output",
            options.ImagemDocker,
            $"whisper-cli -m /models/model.bin -f /audio/input.wav -l {options.Idioma} -mc 0 -sns -np -otxt -of /output/{nomeSaida}"
        ];
    }
}
