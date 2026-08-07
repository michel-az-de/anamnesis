namespace Anamnesis.Infrastructure.Configuracao;

public sealed record ConfiguracaoAnamnesis
{
    public string CaminhoBanco { get; init; } = string.Empty;
    public string DiretorioArquivo { get; init; } = string.Empty;
    public string EnderecoObs { get; init; } = "ws://127.0.0.1:4455";
    public string? SenhaObs { get; init; }
    public string? CaminhoExecutavelObs { get; init; }
    public string CaminhoExecutavelFfmpeg { get; init; } = string.Empty;
    public string CaminhoExecutavelWhisper { get; init; } = string.Empty;
    public string CaminhoModeloWhisper { get; init; } = string.Empty;
    public string IdiomaWhisper { get; init; } = "pt";
    public string? ImagemDockerWhisper { get; init; }
    public string? CaminhoExecutavelDockerDesktop { get; init; }
    public string NomeCli { get; init; } = "CLI configurada";
    public string CaminhoExecutavelCli { get; init; } = string.Empty;
    public IReadOnlyList<string> ArgumentosCli { get; init; } = [];
    public string? ArgumentoArquivoSaidaCli { get; init; }
    public int RetencaoEventosDias { get; init; } = 14;
    public DeteccaoLocalOptions Deteccao { get; init; } = DeteccaoLocalOptions.Padrao;

    public static ConfiguracaoAnamnesis CriarPadrao()
    {
        var diretorioDados = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Anamnesis");
        return new ConfiguracaoAnamnesis
        {
            CaminhoBanco = Path.Combine(diretorioDados, "anamnesis.db"),
            DiretorioArquivo = Path.Combine(diretorioDados, "arquivo")
        };
    }

    public string? ResolverArgumentoArquivoSaidaCli()
    {
        if (!string.IsNullOrWhiteSpace(ArgumentoArquivoSaidaCli))
        {
            return ArgumentoArquivoSaidaCli;
        }

        return string.Equals(
            Path.GetFileName(CaminhoExecutavelCli),
            "codex.exe",
            StringComparison.OrdinalIgnoreCase)
            ? "--output-last-message"
            : null;
    }
}
