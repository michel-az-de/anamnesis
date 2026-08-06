using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class FluxoOperacionalDesktopTests
{
    [Theory]
    [InlineData("Gravando", "Gravando")]
    [InlineData("Aguardando processamento", "NaFila")]
    [InlineData("Transcrevendo", "Transcrevendo")]
    [InlineData("Gerando ata", "GerandoAta")]
    [InlineData("Arquivando", "Arquivando")]
    [InlineData("Ata pronta", "Concluido")]
    [InlineData("Falha", "Falha")]
    public void DeveMapearSomenteEtapasVerificaveis(string status, string etapaEsperada)
    {
        var estado = FluxoOperacionalDesktop.Criar(status);
        var esperada = Enum.Parse<EtapaFluxoOperacional>(etapaEsperada);

        Assert.Equal(esperada, estado.Atual);
        Assert.Equal("Falha", estado.Itens[^1].Nome);
        Assert.Equal(EstadoItemEtapa.Atual, estado.Itens.Single(item => item.Etapa == esperada).Estado);
        Assert.Equal(esperada is not EtapaFluxoOperacional.Concluido and not EtapaFluxoOperacional.Falha, estado.Carregando);
    }

    [Fact]
    public void FalhaDeveManterEtapasAnterioresSemInventarOndeOFluxoParou()
    {
        var estado = FluxoOperacionalDesktop.Criar("Falha");

        Assert.All(
            estado.Itens.Where(item => item.Etapa != EtapaFluxoOperacional.Falha),
            item => Assert.Equal(EstadoItemEtapa.Pendente, item.Estado));
        Assert.Equal(EstadoItemEtapa.Atual, estado.Itens[^1].Estado);
    }
}
