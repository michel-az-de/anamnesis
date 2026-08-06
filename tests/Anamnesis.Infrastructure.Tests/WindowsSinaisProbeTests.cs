using System.ComponentModel;
using Anamnesis.Infrastructure.Deteccao;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class WindowsSinaisProbeTests
{
    [Fact]
    public void FalhaDeEnumWindowsDeveInvalidarAColeta()
    {
        var falha = Assert.Throws<Win32Exception>(() =>
            WindowsSinaisProbe.ValidarResultadoEnumeracao(
                enumeracaoConcluida: false,
                codigoErro: 5));

        Assert.Equal(5, falha.NativeErrorCode);
    }

    [Fact]
    public void EnumeracaoConcluidaNaoDeveFalhar()
    {
        WindowsSinaisProbe.ValidarResultadoEnumeracao(
            enumeracaoConcluida: true,
            codigoErro: 0);
    }
}
