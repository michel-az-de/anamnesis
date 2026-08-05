namespace Anamnesis.Infrastructure.Configuracao;

public static class DiagnosticosLocais
{
    public static IReadOnlyList<DiagnosticoLocal> Avaliar(ConfiguracaoAnamnesis configuracao) =>
    [
        AvaliarObs(configuracao.EnderecoObs),
        AvaliarArquivo("FFmpeg", configuracao.CaminhoExecutavelFfmpeg),
        AvaliarWhisper(configuracao),
        AvaliarArquivo("Modelo Whisper", configuracao.CaminhoModeloWhisper),
        AvaliarArquivo("CLI de LLM", configuracao.CaminhoExecutavelCli)
    ];

    private static DiagnosticoLocal AvaliarObs(string endereco) =>
        Uri.TryCreate(endereco, UriKind.Absolute, out var uri) && (uri.Scheme == "ws" || uri.Scheme == "wss")
            ? new DiagnosticoLocal("OBS", true, "Endereço OBS configurado.")
            : new DiagnosticoLocal("OBS", false, "Endereço OBS inválido.");

    private static DiagnosticoLocal AvaliarWhisper(ConfiguracaoAnamnesis configuracao)
    {
        if (!File.Exists(configuracao.CaminhoExecutavelWhisper))
        {
            return new DiagnosticoLocal("Whisper CLI", false, "Arquivo não encontrado.");
        }

        return string.IsNullOrWhiteSpace(configuracao.ImagemDockerWhisper)
            ? new DiagnosticoLocal("Whisper CLI", true, "Encontrado.")
            : new DiagnosticoLocal("Whisper CLI", true, "Executor Docker e imagem configurados.");
    }

    private static DiagnosticoLocal AvaliarArquivo(string nome, string caminho) =>
        File.Exists(caminho)
            ? new DiagnosticoLocal(nome, true, "Encontrado.")
            : new DiagnosticoLocal(nome, false, "Arquivo não encontrado.");
}
