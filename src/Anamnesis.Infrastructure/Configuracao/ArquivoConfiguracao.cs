using System.Text.Json;

namespace Anamnesis.Infrastructure.Configuracao;

public sealed class ArquivoConfiguracao(string caminhoArquivo)
{
    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        WriteIndented = true
    };

    public async Task<ConfiguracaoAnamnesis> CarregarAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(caminhoArquivo))
        {
            var configuracao = ConfiguracaoAnamnesis.CriarPadrao();
            await SalvarAsync(configuracao, cancellationToken);
            return configuracao;
        }

        await using var arquivo = File.OpenRead(caminhoArquivo);
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
        var diretorio = Path.GetDirectoryName(caminhoArquivo)
            ?? throw new InvalidOperationException("O arquivo de configuração não possui diretório.");
        Directory.CreateDirectory(diretorio);
        await using var arquivo = File.Create(caminhoArquivo);
        var protegida = configuracao with { SenhaObs = SegredoLocal.Proteger(configuracao.SenhaObs) };
        await JsonSerializer.SerializeAsync(arquivo, protegida, OpcoesJson, cancellationToken);
    }
}
