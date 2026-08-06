namespace Anamnesis.Infrastructure.Configuracao;

using Anamnesis.Infrastructure.Obs;

public static class DiagnosticosLocais
{
    public static IReadOnlyList<DiagnosticoLocal> Avaliar(ConfiguracaoAnamnesis configuracao) =>
    [
        AvaliarObs(configuracao),
        AvaliarArquivo("FFmpeg", configuracao.CaminhoExecutavelFfmpeg),
        AvaliarWhisper(configuracao),
        AvaliarArquivo("Modelo Whisper", configuracao.CaminhoModeloWhisper),
        AvaliarArquivo("CLI de LLM", configuracao.CaminhoExecutavelCli)
    ];

    private static DiagnosticoLocal AvaliarObs(ConfiguracaoAnamnesis configuracao)
    {
        if (!Uri.TryCreate(configuracao.EnderecoObs, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "ws" && uri.Scheme != "wss"))
        {
            return new DiagnosticoLocal("OBS", false, "Endereço OBS inválido.");
        }

        var executavel = ObsProcessPreflight.ResolverCaminhoExecutavel(configuracao.CaminhoExecutavelObs);
        return File.Exists(executavel)
            ? new DiagnosticoLocal("OBS", true, "Executável encontrado; WebSocket será validado ao gravar.")
            : new DiagnosticoLocal("OBS", false, "Executável do OBS não encontrado.");
    }

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
