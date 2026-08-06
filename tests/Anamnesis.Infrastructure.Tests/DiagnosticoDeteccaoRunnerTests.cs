using System.Text.Json;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class DiagnosticoDeteccaoRunnerTests
{
    [Fact]
    public async Task DeveGerarJsonlSeguroEmWriterInjetado()
    {
        using var saida = new StringWriter();
        var options = new DiagnosticoDeteccaoOptions(
            1,
            TimeSpan.FromMilliseconds(100),
            CaminhoSaida: null);

        var codigoSaida = await DiagnosticoDeteccaoRunner.ExecutarAsync(
            new FonteFake(),
            ModoDeteccaoReuniao.Assistido,
            options,
            saida,
            CancellationToken.None);

        Assert.Equal(0, codigoSaida);
        var linhas = saida.ToString().Split(
            Environment.NewLine,
            StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, linhas.Length);
        Assert.All(linhas, linha => JsonDocument.Parse(linha).Dispose());
        Assert.Contains("diagnostico_deteccao_iniciado", linhas[0], StringComparison.Ordinal);
        Assert.Contains("browser_meet", linhas[1], StringComparison.Ordinal);
        Assert.Contains("diagnostico_deteccao_concluido", linhas[2], StringComparison.Ordinal);
        using var amostra = JsonDocument.Parse(linhas[1]);
        Assert.True(amostra.RootElement.GetProperty("coletaConfiavel").GetBoolean());
        Assert.DoesNotContain("titulo-confidencial", saida.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FonteFake : ISinaisReuniaoSource
    {
        public Task<SinaisDeteccaoReuniao> ObterAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SinaisDeteccaoReuniao(
                MicrofoneAtivo: true,
                AudioSaidaAtivo: true,
                new PlataformaLocal(
                    "browser_meet",
                    "Google Meet",
                    OrigemPlataformaLocal.Navegador),
                EventoAgendaProximo: false,
                Ambiguo: false));
    }
}
