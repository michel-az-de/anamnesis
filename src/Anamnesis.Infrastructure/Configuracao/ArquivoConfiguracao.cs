using System.Text.Json;

namespace Anamnesis.Infrastructure.Configuracao;

public sealed class ArquivoConfiguracao
{
    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        WriteIndented = true
    };
    private readonly string _caminhoArquivo;
    private readonly Func<ConfiguracaoAnamnesis> _criarPadrao;

    public ArquivoConfiguracao(string caminhoArquivo)
        : this(caminhoArquivo, new DescobertaConfiguracaoLocal().Criar)
    {
    }

    internal ArquivoConfiguracao(
        string caminhoArquivo,
        Func<ConfiguracaoAnamnesis> criarPadrao)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoArquivo);
        _caminhoArquivo = Path.GetFullPath(caminhoArquivo);
        _criarPadrao = criarPadrao ?? throw new ArgumentNullException(nameof(criarPadrao));
    }

    public async Task<ConfiguracaoAnamnesis> CarregarAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_caminhoArquivo))
        {
            var configuracao = _criarPadrao();
            await SalvarAsync(configuracao, cancellationToken);
            return configuracao;
        }

        await using var arquivo = File.OpenRead(_caminhoArquivo);
        var lida = await JsonSerializer.DeserializeAsync<ConfiguracaoAnamnesis>(arquivo, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("O arquivo de configuração está vazio ou inválido.");
        return lida with
        {
            SenhaObs = SegredoLocal.Revelar(lida.SenhaObs),
            Deteccao = lida.Deteccao ?? DeteccaoLocalOptions.Padrao
        };
    }

    public async Task SalvarAsync(ConfiguracaoAnamnesis configuracao, CancellationToken cancellationToken)
    {
        var diretorio = Path.GetDirectoryName(_caminhoArquivo)
            ?? throw new InvalidOperationException("O arquivo de configuração não possui diretório.");
        Directory.CreateDirectory(diretorio);
        await using var arquivo = File.Create(_caminhoArquivo);
        var protegida = configuracao with { SenhaObs = SegredoLocal.Proteger(configuracao.SenhaObs) };
        await JsonSerializer.SerializeAsync(arquivo, protegida, OpcoesJson, cancellationToken);
    }
}
