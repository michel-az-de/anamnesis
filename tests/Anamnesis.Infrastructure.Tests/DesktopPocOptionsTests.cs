using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class DesktopPocOptionsTests
{
    [Fact]
    public void DeveManterFluxoNormalSemArgumento()
    {
        Assert.False(DesktopPocOptions.EstaAtivo([]));
    }

    [Fact]
    public void DeveReconhecerModoDesktopSemDiferenciarMaiusculas()
    {
        Assert.True(DesktopPocOptions.EstaAtivo(["--POC-DESKTOP"]));
    }
}

