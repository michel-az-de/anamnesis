using Anamnesis.Infrastructure.Audio;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class WindowsNivelAudioSourceTests
{
    [Theory]
    [InlineData(0F, 0)]
    [InlineData(0.004F, 0)]
    [InlineData(0.501F, 50)]
    [InlineData(1F, 100)]
    [InlineData(2F, 100)]
    [InlineData(-1F, 0)]
    public void DeveNormalizarPicoParaEscalaVisual(float pico, int esperado)
    {
        Assert.Equal(esperado, WindowsNivelAudioSource.NormalizarPico(pico));
    }

    [Fact]
    public void PicoInvalidoDeveResultarEmSemLeitura()
    {
        Assert.Null(WindowsNivelAudioSource.NormalizarPico(float.NaN));
        Assert.Null(WindowsNivelAudioSource.NormalizarPico(float.PositiveInfinity));
    }
}
