using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class DesktopPocThemeTests
{
    [Theory]
    [InlineData(SystemColorMode.Dark, TemaDesktopPoc.Escuro)]
    [InlineData(SystemColorMode.Classic, TemaDesktopPoc.Claro)]
    [InlineData(SystemColorMode.System, TemaDesktopPoc.Claro)]
    public void DeveInterpretarTemaDoWindows(SystemColorMode modoWindows, TemaDesktopPoc esperado)
    {
        Assert.Equal(esperado, DesktopPocTheme.Interpretar(modoWindows));
    }

    [Fact]
    public void DeveCriarPaletaEscuraComContrasteEntreFundoETexto()
    {
        var paleta = DesktopPocPalette.Criar(TemaDesktopPoc.Escuro);

        Assert.True(paleta.Fundo.GetBrightness() < 0.2F);
        Assert.True(paleta.Superficie.GetBrightness() < 0.25F);
        Assert.True(paleta.Texto.GetBrightness() > 0.7F);
        Assert.NotEqual(paleta.Fundo, paleta.Superficie);
        Assert.NotEqual(paleta.Texto, paleta.TextoSecundario);
        Assert.True(paleta.ConsoleFundo.GetBrightness() < paleta.Superficie.GetBrightness());
        Assert.True(paleta.ConsoleTexto.GetBrightness() > 0.65F);
    }

    [Fact]
    public void DeveManterPaletaClaraDisponivel()
    {
        var paleta = DesktopPocPalette.Criar(TemaDesktopPoc.Claro);

        Assert.True(paleta.Fundo.GetBrightness() > 0.8F);
        Assert.True(paleta.Texto.GetBrightness() < 0.25F);
        Assert.True(paleta.ConsoleFundo.GetBrightness() < 0.2F);
        Assert.True(paleta.ConsoleTexto.GetBrightness() > 0.65F);
    }
}
