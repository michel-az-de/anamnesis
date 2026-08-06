using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class DiagnosticoDeteccaoOptionsTests
{
    [Fact]
    public void DevePermanecerInativoSemArgumento()
    {
        Assert.Null(DiagnosticoDeteccaoOptions.Obter([]));
    }

    [Fact]
    public void DeveLerAmostrasEIntervaloComLimitesSeguros()
    {
        var options = DiagnosticoDeteccaoOptions.Obter(
            ["--diagnostico-deteccao", "--amostras", "3", "--intervalo-ms", "250"]);

        Assert.NotNull(options);
        Assert.Equal(3, options!.Amostras);
        Assert.Equal(TimeSpan.FromMilliseconds(250), options.Intervalo);
    }

    [Fact]
    public void ValoresExtremosDevemSerLimitados()
    {
        var options = DiagnosticoDeteccaoOptions.Obter(
            ["--diagnostico-deteccao", "--amostras", "999", "--intervalo-ms", "1"]);

        Assert.Equal(60, options!.Amostras);
        Assert.Equal(TimeSpan.FromMilliseconds(100), options.Intervalo);
    }

    [Fact]
    public void DeveAceitarArquivoDeSaidaParaExecutavelWinExe()
    {
        var options = DiagnosticoDeteccaoOptions.Obter(
            ["--diagnostico-deteccao", "--saida", "diagnostico.jsonl"]);

        Assert.Equal("diagnostico.jsonl", options!.CaminhoSaida);
    }
}
