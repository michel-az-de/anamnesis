using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class WindowsTitleBarThemeTests
{
    [Theory]
    [InlineData(10, 0, 19045)]
    [InlineData(10, 0, 22000)]
    [InlineData(10, 0, 22621)]
    [InlineData(10, 0, 26100)]
    public void DeveManterBackdropDesativadoEmTodasAsVersoes(int major, int minor, int build)
    {
        Assert.Equal(1, WindowsTitleBarTheme.ObterTipoBackdrop(new Version(major, minor, build)));
    }

    [Fact]
    public void DeveConverterCorParaColorRefDoDwm()
    {
        var cor = Color.FromArgb(0x12, 0x34, 0x56);

        Assert.Equal(0x00563412, WindowsTitleBarTheme.ConverterColorRef(cor));
    }
}
