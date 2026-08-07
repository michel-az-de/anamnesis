using Anamnesis.Infrastructure.Arquivos;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class ObsidianPublisherTests : IDisposable
{
    private readonly string _diretorio = Path.Combine(
        Path.GetTempPath(),
        $"anamnesis-obsidian-{Guid.NewGuid():N}");

    [Fact]
    public async Task DevePublicarMarkdownIdempotenteComPropriedadesEConteudo()
    {
        var vault = CriarVault();
        var detalhe = AtaDocumentoExporterTests.CriarDetalhe();
        var publicador = new ObsidianPublisher();

        var primeiro = await publicador.PublicarAsync(
            detalhe,
            vault,
            "Anamnesis/Reuniões",
            CancellationToken.None);
        var conteudoInicial = await File.ReadAllTextAsync(primeiro);
        await File.AppendAllTextAsync(primeiro, "\nEdição manual preservada.\n");
        var segundo = await publicador.PublicarAsync(
            detalhe,
            vault,
            "Anamnesis/Reuniões",
            CancellationToken.None);

        Assert.Equal(primeiro, segundo);
        var conteudoFinal = await File.ReadAllTextAsync(segundo);
        Assert.StartsWith(conteudoInicial, conteudoFinal, StringComparison.Ordinal);
        Assert.Contains("Edição manual preservada.", conteudoFinal, StringComparison.Ordinal);
        Assert.Contains(Path.Combine("2026", "08"), primeiro, StringComparison.Ordinal);
        Assert.Contains($"anamnesis_id: {detalhe.Id:N}", conteudoInicial, StringComparison.Ordinal);
        Assert.Contains("## Resumo executivo", conteudoInicial, StringComparison.Ordinal);
        Assert.Contains("- [ ] Enviar o relatório.", conteudoInicial, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../fora")]
    [InlineData(".obsidian/plugins")]
    public async Task DeveRejeitarDestinoForaDoVaultOuNaConfiguracao(string subpasta)
    {
        var vault = CriarVault();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ObsidianPublisher().PublicarAsync(
            AtaDocumentoExporterTests.CriarDetalhe(),
            vault,
            subpasta,
            CancellationToken.None));
    }

    [Fact]
    public async Task DeveExigirMarcadorDeVaultObsidian()
    {
        Directory.CreateDirectory(_diretorio);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ObsidianPublisher().PublicarAsync(
            AtaDocumentoExporterTests.CriarDetalhe(),
            _diretorio,
            "Anamnesis",
            CancellationToken.None));
    }

    [Fact]
    public async Task ReparsePointCriadoAntesDoMovimentoDeveInterromperPublicacao()
    {
        var vault = CriarVault();
        var simularReparse = false;
        var publicador = new ObsidianPublisher(
            caminho =>
                simularReparse && caminho.EndsWith("Reunioes", StringComparison.OrdinalIgnoreCase)
                    ? FileAttributes.Directory | FileAttributes.ReparsePoint
                    : File.GetAttributes(caminho),
            _ => simularReparse = true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => publicador.PublicarAsync(
            AtaDocumentoExporterTests.CriarDetalhe(),
            vault,
            "Anamnesis/Reunioes",
            CancellationToken.None));

        Assert.Empty(Directory.EnumerateFiles(
            Path.Combine(vault, "Anamnesis", "Reunioes"),
            "*.md",
            SearchOption.AllDirectories));
    }

    [Fact]
    public void PublicadorNaoDeveDependerDeRetencaoOuCaminhoDeGravacao()
    {
        var tiposDasDependencias = typeof(ObsidianPublisher)
            .GetConstructors(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic)
            .SelectMany(construtor => construtor.GetParameters())
            .Select(parametro => parametro.ParameterType.FullName ?? parametro.ParameterType.Name);

        Assert.DoesNotContain(
            tiposDasDependencias,
            tipo => tipo.Contains("Retencao", StringComparison.OrdinalIgnoreCase) ||
                    tipo.Contains("Gravacao", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_diretorio))
        {
            Directory.Delete(_diretorio, recursive: true);
        }
    }

    private string CriarVault()
    {
        Directory.CreateDirectory(Path.Combine(_diretorio, ".obsidian"));
        return _diretorio;
    }
}
