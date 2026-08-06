using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Anamnesis.Application.UseCases;
using Xunit;

namespace Anamnesis.Application.Tests;

public sealed class DetectorReuniaoTests
{
    private static readonly PlataformaLocal Meet = new(
        "browser_meet",
        "Google Meet",
        OrigemPlataformaLocal.Navegador);

    [Fact]
    public async Task DeveRegistrarSomenteTransicoesSegurasSemNomeDaPlataforma()
    {
        var relogio = new RelogioControlado();
        var sink = new EventoSinkFake();
        var detector = new DetectorReuniao(
            new FonteFake(Chamada()),
            new PoliticaDeteccaoReuniao(
                PoliticaDeteccaoOptions.Padrao with { Modo = ModoDeteccaoReuniao.Automatico }),
            relogio,
            new JornalOperacional(sink, relogio));

        await detector.AvaliarAsync(reuniaoAtivaId: null, CancellationToken.None);
        relogio.Avancar(TimeSpan.FromSeconds(5));
        var contagem = await detector.AvaliarAsync(
            reuniaoAtivaId: null,
            CancellationToken.None);
        relogio.Avancar(TimeSpan.FromSeconds(1));
        await detector.AvaliarAsync(reuniaoAtivaId: null, CancellationToken.None);

        Assert.Equal(TipoDecisaoDeteccao.IniciarContagem, contagem.Tipo);
        var evento = Assert.Single(sink.Eventos);
        Assert.Equal(CodigosEventoOperacional.DeteccaoDecidida, evento.Codigo);
        Assert.Equal("Detector", evento.Componente);
        Assert.Equal("iniciar_contagem", evento.Metadados.Resultado);
        Assert.Equal("sinais_sustentados", evento.Metadados.MotivoCodigo);
        Assert.DoesNotContain("Meet", evento.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("browser_meet", evento.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FalhaTotalDaFonteDeveReduzirConfiancaSemInterromperFluxo()
    {
        var relogio = new RelogioControlado();
        var sink = new EventoSinkFake();
        var detector = new DetectorReuniao(
            new FonteComFalha(),
            new PoliticaDeteccaoReuniao(PoliticaDeteccaoOptions.Padrao),
            relogio,
            new JornalOperacional(sink, relogio));

        var decisao = await detector.AvaliarAsync(
            reuniaoAtivaId: null,
            CancellationToken.None);

        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, decisao.Tipo);
        var falha = Assert.Single(
            sink.Eventos,
            evento => evento.Codigo == CodigosEventoOperacional.OperacaoFalhou);
        Assert.Equal(CodigosEventoOperacional.OperacaoFalhou, falha.Codigo);
        Assert.Equal("Detector", falha.Componente);
        Assert.Equal("coletar_sinais", falha.Metadados.Operacao);
        Assert.DoesNotContain("Janela privada", falha.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Single(
            sink.Eventos,
            evento => evento.Metadados.Resultado == "degradada");
    }

    [Fact]
    public async Task FalhaDaFonteDuranteContagemDeveCancelarERegistrarSaudeUmaVez()
    {
        var relogio = new RelogioControlado();
        var sink = new EventoSinkFake();
        var fonte = new FonteAlternavel(Chamada());
        var detector = new DetectorReuniao(
            fonte,
            new PoliticaDeteccaoReuniao(
                PoliticaDeteccaoOptions.Padrao with { Modo = ModoDeteccaoReuniao.Automatico }),
            relogio,
            new JornalOperacional(sink, relogio));

        await detector.AvaliarAsync(reuniaoAtivaId: null, CancellationToken.None);
        relogio.Avancar(TimeSpan.FromSeconds(5));
        await detector.AvaliarAsync(reuniaoAtivaId: null, CancellationToken.None);
        fonte.Falhar = true;
        var cancelada = await detector.AvaliarAsync(
            reuniaoAtivaId: null,
            CancellationToken.None);
        await detector.AvaliarAsync(reuniaoAtivaId: null, CancellationToken.None);
        fonte.Falhar = false;
        await detector.AvaliarAsync(reuniaoAtivaId: null, CancellationToken.None);

        Assert.Equal(TipoDecisaoDeteccao.CancelarContagem, cancelada.Tipo);
        Assert.Single(sink.Eventos, evento => evento.Metadados.Resultado == "degradada");
        Assert.Single(sink.Eventos, evento => evento.Metadados.Resultado == "restaurada");
        Assert.Single(sink.Eventos, evento =>
            evento.Metadados.Resultado == "cancelar_contagem" &&
            evento.Metadados.MotivoCodigo == "coleta_indisponivel");
    }

    [Fact]
    public async Task IgnorarESilenciarDevemGerarDecisoesSeguras()
    {
        var relogio = new RelogioControlado();
        var sink = new EventoSinkFake();
        var detector = new DetectorReuniao(
            new FonteFake(Chamada()),
            new PoliticaDeteccaoReuniao(PoliticaDeteccaoOptions.Padrao),
            relogio,
            new JornalOperacional(sink, relogio));

        await detector.AvaliarAsync(reuniaoAtivaId: null, CancellationToken.None);
        await detector.IgnorarAsync(CancellationToken.None);
        await detector.SilenciarAsync(CancellationToken.None);

        Assert.Equal(3, sink.Eventos.Count);
        Assert.All(
            sink.Eventos,
            evento => Assert.Equal(CodigosEventoOperacional.DeteccaoDecidida, evento.Codigo));
        Assert.Equal(
            ["sugerir_inicio", "ignorar", "silenciar"],
            sink.Eventos.Select(evento => evento.Metadados.Resultado));
    }

    private static SinaisDeteccaoReuniao Chamada() =>
        new(true, true, Meet, false, false);

    private sealed class FonteFake(SinaisDeteccaoReuniao sinais) : ISinaisReuniaoSource
    {
        public Task<SinaisDeteccaoReuniao> ObterAsync(CancellationToken cancellationToken) =>
            Task.FromResult(sinais);
    }

    private sealed class FonteComFalha : ISinaisReuniaoSource
    {
        public Task<SinaisDeteccaoReuniao> ObterAsync(CancellationToken cancellationToken) =>
            Task.FromException<SinaisDeteccaoReuniao>(
                new InvalidOperationException("Janela privada do cliente X."));
    }

    private sealed class FonteAlternavel(SinaisDeteccaoReuniao sinais) : ISinaisReuniaoSource
    {
        public bool Falhar { get; set; }

        public Task<SinaisDeteccaoReuniao> ObterAsync(CancellationToken cancellationToken) =>
            Falhar
                ? Task.FromException<SinaisDeteccaoReuniao>(
                    new InvalidOperationException("Probe temporariamente indisponível."))
                : Task.FromResult(sinais);
    }

    private sealed class RelogioControlado : TimeProvider
    {
        private DateTimeOffset _agora =
            new(2026, 8, 5, 18, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan duracao) => _agora = _agora.Add(duracao);
    }

    private sealed class EventoSinkFake : IEventoOperacionalSink
    {
        public List<EventoOperacional> Eventos { get; } = [];

        public Task RegistrarAsync(EventoOperacional evento, CancellationToken cancellationToken)
        {
            Eventos.Add(evento);
            return Task.CompletedTask;
        }

        public Task RemoverAnterioresAsync(
            DateTimeOffset limiteUtc,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
