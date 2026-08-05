namespace Anamnesis.Infrastructure.Configuracao;

public sealed record ConfiguracaoAnamnesis
{
    public string CaminhoBanco { get; init; } = string.Empty;
    public string DiretorioArquivo { get; init; } = string.Empty;
    public string EnderecoObs { get; init; } = "ws://127.0.0.1:4455";
    public string? SenhaObs { get; init; }
    public string CaminhoExecutavelFfmpeg { get; init; } = string.Empty;
    public string CaminhoExecutavelWhisper { get; init; } = string.Empty;
    public string CaminhoModeloWhisper { get; init; } = string.Empty;
    public string IdiomaWhisper { get; init; } = "pt";
    public string? ImagemDockerWhisper { get; init; }
    public string NomeCli { get; init; } = "CLI configurada";
    public string CaminhoExecutavelCli { get; init; } = string.Empty;
    public IReadOnlyList<string> ArgumentosCli { get; init; } = [];

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
}
