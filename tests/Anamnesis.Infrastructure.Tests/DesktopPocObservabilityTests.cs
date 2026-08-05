using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class DesktopPocObservabilityTests
{
    [Fact]
    public void ModoRealDeveComecarVazioERegistrarSomenteFalhaLocalSegura()
    {
        var state = new DesktopPocObservabilityState(incluirHistorico: false);

        Assert.Empty(state.Eventos);

        state.RegistrarFalhaLocal("Desktop", "artefato.falhou", "Falha segura\r\nsem caminho");

        var evento = Assert.Single(state.Eventos);
        Assert.Equal(NivelEventoPoc.Erro, evento.Nivel);
        Assert.Equal("Desktop", evento.Componente);
        Assert.Equal("artefato.falhou", evento.Evento);
        Assert.Equal("Falha segura  sem caminho", evento.Mensagem);
        Assert.Equal("desktop-local", evento.CorrelacaoId);
    }

    [Fact]
    public void DeveIniciarComEventosEstruturadosESeguros()
    {
        var state = new DesktopPocObservabilityState(() => new DateTimeOffset(2026, 8, 5, 14, 0, 0, TimeSpan.FromHours(-3)));

        Assert.True(state.Eventos.Count >= 5);
        Assert.Contains(state.Eventos, evento => evento.Nivel == NivelEventoPoc.Aviso);
        Assert.All(state.Eventos, evento =>
        {
            Assert.False(string.IsNullOrWhiteSpace(evento.Componente));
            Assert.False(string.IsNullOrWhiteSpace(evento.Evento));
            Assert.False(string.IsNullOrWhiteSpace(evento.CorrelacaoId));
        });

        var diagnostico = string.Join('\n', state.Eventos.Select(evento => evento.Mensagem));
        Assert.DoesNotContain("C:\\Users", diagnostico, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token=", diagnostico, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Felipe", diagnostico, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Vamos planejar", diagnostico, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeveFiltrarPorTextoNivelEComponenteSemAlterarEventos()
    {
        var state = new DesktopPocObservabilityState();
        var totalOriginal = state.Eventos.Count;

        var avisosObs = state.Filtrar("nível", NivelEventoPoc.Aviso, "OBS");

        Assert.NotEmpty(avisosObs);
        Assert.All(avisosObs, evento =>
        {
            Assert.Equal(NivelEventoPoc.Aviso, evento.Nivel);
            Assert.Equal("OBS", evento.Componente);
            Assert.Contains("nível", evento.Mensagem, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Equal(totalOriginal, state.Eventos.Count);
    }

    [Fact]
    public void DeveRegistrarInicioDaGravacaoComMesmaCorrelacao()
    {
        var state = new DesktopPocObservabilityState();

        state.RegistrarInicioGravacao();

        var novosEventos = state.Eventos.Take(2).ToArray();
        Assert.Equal(2, novosEventos.Length);
        Assert.All(novosEventos, evento => Assert.Equal(novosEventos[0].CorrelacaoId, evento.CorrelacaoId));
        Assert.Contains(novosEventos, evento => evento.Componente == "Tray" && evento.Evento == "gravacao.iniciada");
        Assert.Contains(novosEventos, evento => evento.Componente == "OBS" && evento.Evento == "captura.saudavel");
    }

    [Fact]
    public void DeveMedirFilaAoEncerrarGravacao()
    {
        var state = new DesktopPocObservabilityState();
        state.RegistrarInicioGravacao();

        state.RegistrarFimGravacao(TimeSpan.FromSeconds(12));

        Assert.Equal(1, state.Metricas.JobsNaFila);
        Assert.Contains(state.Eventos, evento => evento.Componente == "Fila" && evento.Evento == "job.criado");
        Assert.Contains(state.Eventos, evento => evento.Componente == "Whisper" && evento.Evento == "transcricao.iniciada");
    }

    [Fact]
    public void DeveAtualizarMetricasAoConcluirProcessamento()
    {
        var state = new DesktopPocObservabilityState();
        state.RegistrarInicioGravacao();
        state.RegistrarFimGravacao(TimeSpan.FromSeconds(12));
        var totalAntes = state.Metricas.TotalEventos;

        state.RegistrarConclusaoProcessamento();

        Assert.Equal(0, state.Metricas.JobsNaFila);
        Assert.True(state.Metricas.TotalEventos >= totalAntes + 4);
        Assert.True(state.Metricas.DuracaoMediaMs > 0);
        Assert.Contains(state.Eventos, evento => evento.Componente == "Worker" && evento.Evento == "job.concluido");
        Assert.Contains(state.Eventos, evento => evento.Componente == "Arquivo" && evento.Evento == "reuniao.arquivada");
    }
}
