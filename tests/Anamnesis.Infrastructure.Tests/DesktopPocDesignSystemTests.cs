using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class DesktopPocDesignSystemTests
{
    [Fact]
    public void DeveDefinirEscalasSemanticasCompletas()
    {
        var tokens = DesktopPocDesignTokens.Padrao;

        Assert.Equal([4, 8, 12, 16, 24, 32], tokens.Espacamento.Escala);
        Assert.Equal([6, 8, 12, 16], tokens.Geometria.Raios);
        Assert.Equal(1, tokens.Geometria.Borda);
        Assert.Equal(2, tokens.Geometria.BordaFoco);
        Assert.Equal(83, tokens.Motion.FasterMs);
        Assert.Equal(167, tokens.Motion.FastMs);
        Assert.Equal(250, tokens.Motion.NormalMs);
        Assert.Equal(24, tokens.Motion.DeslocamentoPagina);
        Assert.False(string.IsNullOrWhiteSpace(tokens.Tipografia.Interface));
        Assert.False(string.IsNullOrWhiteSpace(tokens.Tipografia.Display));
        Assert.False(string.IsNullOrWhiteSpace(tokens.Tipografia.Mono));
    }

    [Fact]
    public void DeveDefinirSuperficiesSolidasNosDoisTemas()
    {
        foreach (var tema in new[] { TemaDesktopPoc.Escuro, TemaDesktopPoc.Claro })
        {
            var superficies = DesktopPocPalette.Criar(tema).Superficies;

            Assert.All(
                new[]
                {
                    superficies.Canvas,
                    superficies.Painel,
                    superficies.PainelElevado,
                    superficies.PainelHover,
                    superficies.BordaForte,
                    superficies.DestaqueSuave,
                    superficies.PositivoSuave
                },
                cor => Assert.Equal(255, cor.A));
            Assert.NotEqual(superficies.Canvas, superficies.Painel);
            Assert.NotEqual(superficies.Painel, superficies.PainelElevado);
        }
    }

    [Fact]
    public void DeveManterFundosOperacionaisSemAlpha()
    {
        foreach (var tema in new[] { TemaDesktopPoc.Escuro, TemaDesktopPoc.Claro })
        {
            var paleta = DesktopPocPalette.Criar(tema);

            Assert.All(
                new[]
                {
                    paleta.Fundo,
                    paleta.Superficie,
                    paleta.Navegacao,
                    paleta.Selecao,
                    paleta.FundoDestaque,
                    paleta.FundoPositivo,
                    paleta.ConsoleFundo,
                    paleta.ConsoleLinha
                },
                cor => Assert.Equal(255, cor.A));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void DeveInterpretarPreferenciasVisuaisDoWindows(
        bool animacoesAreaCliente,
        bool animacoesEsperadas)
    {
        var politica = DesktopPocSystemPreferences.Interpretar(animacoesAreaCliente);

        Assert.Equal(animacoesEsperadas, politica.AnimacoesAtivas);
    }

    [Fact]
    public void DeveUsarCurvaDeDesaceleracaoEInterpolacaoDeterministicas()
    {
        Assert.Equal(0D, DesktopPocMotion.SuavizarSaida(0D), 6);
        Assert.Equal(0.875D, DesktopPocMotion.SuavizarSaida(0.5D), 6);
        Assert.Equal(1D, DesktopPocMotion.SuavizarSaida(1D), 6);
        Assert.Equal(24, DesktopPocMotion.CalcularDeslocamento(0D, 24));
        Assert.Equal(3, DesktopPocMotion.CalcularDeslocamento(0.5D, 24));
        Assert.Equal(0, DesktopPocMotion.CalcularDeslocamento(1D, 24));

        var meio = DesktopPocMotion.Misturar(Color.Black, Color.White, 0.5D);
        Assert.Equal(Color.FromArgb(255, 128, 128, 128), meio);
    }
}
