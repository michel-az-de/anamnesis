using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.UseCases;
using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class CapturaInstantaneaControllerTests
{
    private static readonly PlataformaLocal Meet = new(
        "browser_meet",
        "Google Meet",
        OrigemPlataformaLocal.Navegador);

    [Fact]
    public async Task AssistidoDeveIniciarSomenteDepoisDoClique()
    {
        var contexto = Criar(ModoDeteccaoReuniao.Assistido);
        contexto.Prompt.ProximaAcao = AcaoSugestaoDeteccao.Iniciar;

        await contexto.Controller.ProcessarAsync(CancellationToken.None);

        Assert.Equal(1, contexto.Sessao.Inicios);
        Assert.Equal("Reunião detectada • Google Meet", contexto.Sessao.UltimoTitulo);
        Assert.Equal(1, contexto.Prompt.Sugestoes);
    }

    [Fact]
    public async Task AutomaticoDeveMostrarContagemECancelarSemIniciar()
    {
        var contexto = Criar(ModoDeteccaoReuniao.Automatico);

        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromSeconds(5));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Prompt.CancelamentoInicioPendente = true;
        await contexto.Controller.ProcessarAsync(CancellationToken.None);

        Assert.Equal(0, contexto.Sessao.Inicios);
        Assert.Equal(1, contexto.Prompt.ContagensExibidas);
        Assert.Equal(1, contexto.Prompt.ContagensFechadas);
    }

    [Fact]
    public async Task SilenciarDuranteContagemDeveFechaLaSemIniciar()
    {
        var contexto = Criar(ModoDeteccaoReuniao.Automatico);

        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromSeconds(5));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        await contexto.Controller.SilenciarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromMinutes(1));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);

        Assert.Equal(1, contexto.Prompt.AcoesFechadas);
        Assert.Equal(0, contexto.Sessao.Inicios);
    }

    [Fact]
    public async Task SilenciarDuranteAvisoNaoDeveEncerrarInvisivelmente()
    {
        var contexto = Criar(
            ModoDeteccaoReuniao.Automatico,
            SinaisDeteccaoReuniao.Nenhum,
            finalizacaoAutomatica: true);
        contexto.Sessao.Etapa = EtapaDesktopPoc.Gravando;
        contexto.Sessao.ReuniaoAtivaId = Guid.NewGuid();
        contexto.Detector.ConfirmarInicioAutomatico(contexto.Sessao.ReuniaoAtivaId.Value);

        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromMinutes(2));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        await contexto.Controller.SilenciarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromMinutes(59));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromMinutes(2));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);

        Assert.Equal(1, contexto.Prompt.AvisosEncerramento);
        Assert.Equal(1, contexto.Prompt.AcoesFechadas);
        Assert.Equal(0, contexto.Sessao.Encerramentos);
    }

    [Fact]
    public async Task AutomaticoDeveIniciarExatamenteUmaVezENotificar()
    {
        var contexto = Criar(ModoDeteccaoReuniao.Automatico);

        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromSeconds(5));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromSeconds(5));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromMinutes(1));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);

        Assert.Equal(1, contexto.Sessao.Inicios);
        Assert.Equal(1, contexto.Prompt.IniciosNotificados);
        Assert.Equal(EtapaDesktopPoc.Gravando, contexto.Sessao.Etapa);
    }

    [Fact]
    public async Task RecuperacaoPendenteDeveSuspenderDetectorSemLerSinais()
    {
        var contexto = Criar(ModoDeteccaoReuniao.Automatico);
        contexto.Sessao.RecuperacaoPendente = true;

        await contexto.Controller.ProcessarAsync(CancellationToken.None);

        Assert.Equal(0, contexto.Fonte.Leituras);
        Assert.Equal(0, contexto.Sessao.Inicios);
    }

    [Fact]
    public async Task SessaoAindaNaoInicializadaDeveSuspenderDetectorSemLerSinais()
    {
        var contexto = Criar(ModoDeteccaoReuniao.Automatico);
        contexto.Sessao.Inicializada = false;

        await contexto.Controller.ProcessarAsync(CancellationToken.None);

        Assert.Equal(0, contexto.Fonte.Leituras);
        Assert.Equal(0, contexto.Sessao.Inicios);
    }

    [Fact]
    public async Task FinalizacaoAutomaticaDeveAvisarEPermitirCancelamento()
    {
        var contexto = Criar(
            ModoDeteccaoReuniao.Automatico,
            SinaisDeteccaoReuniao.Nenhum,
            finalizacaoAutomatica: true);
        contexto.Sessao.Etapa = EtapaDesktopPoc.Gravando;
        contexto.Sessao.ReuniaoAtivaId = Guid.NewGuid();
        contexto.Detector.ConfirmarInicioAutomatico(contexto.Sessao.ReuniaoAtivaId.Value);

        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromMinutes(2));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Prompt.CancelamentoEncerramentoPendente = true;
        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromMinutes(10));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);

        Assert.Equal(1, contexto.Prompt.AvisosEncerramento);
        Assert.Equal(0, contexto.Sessao.Encerramentos);
    }

    [Fact]
    public async Task FinalizacaoAutomaticaConfirmadaDeveEncerrarUmaVez()
    {
        var contexto = Criar(
            ModoDeteccaoReuniao.Automatico,
            SinaisDeteccaoReuniao.Nenhum,
            finalizacaoAutomatica: true);
        contexto.Sessao.Etapa = EtapaDesktopPoc.Gravando;
        contexto.Sessao.ReuniaoAtivaId = Guid.NewGuid();
        contexto.Detector.ConfirmarInicioAutomatico(contexto.Sessao.ReuniaoAtivaId.Value);

        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromMinutes(2));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromSeconds(15));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        await contexto.Controller.ProcessarAsync(CancellationToken.None);

        Assert.Equal(1, contexto.Sessao.Encerramentos);
        Assert.Equal(EtapaDesktopPoc.Processando, contexto.Sessao.Etapa);
    }

    [Fact]
    public async Task FalhaNoAutoStopDeveSuprimirNovasTentativasAteRetornoDosSinais()
    {
        var contexto = Criar(
            ModoDeteccaoReuniao.Automatico,
            SinaisDeteccaoReuniao.Nenhum,
            finalizacaoAutomatica: true);
        contexto.Sessao.Etapa = EtapaDesktopPoc.Gravando;
        contexto.Sessao.ReuniaoAtivaId = Guid.NewGuid();
        contexto.Sessao.FalharEncerramento = true;
        contexto.Detector.ConfirmarInicioAutomatico(contexto.Sessao.ReuniaoAtivaId.Value);

        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromMinutes(2));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromSeconds(15));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromSeconds(1));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);

        Assert.Equal(1, contexto.Sessao.Encerramentos);
        Assert.Equal(EtapaDesktopPoc.Gravando, contexto.Sessao.Etapa);
    }

    [Fact]
    public async Task RetornoDosSinaisDeveFecharAvisoDeEncerramento()
    {
        var contexto = Criar(
            ModoDeteccaoReuniao.Automatico,
            SinaisDeteccaoReuniao.Nenhum,
            finalizacaoAutomatica: true);
        contexto.Sessao.Etapa = EtapaDesktopPoc.Gravando;
        contexto.Sessao.ReuniaoAtivaId = Guid.NewGuid();
        contexto.Detector.ConfirmarInicioAutomatico(contexto.Sessao.ReuniaoAtivaId.Value);

        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Relogio.Avancar(TimeSpan.FromMinutes(2));
        await contexto.Controller.ProcessarAsync(CancellationToken.None);
        contexto.Fonte.Sinais = new SinaisDeteccaoReuniao(true, true, Meet, false, false);
        await contexto.Controller.ProcessarAsync(CancellationToken.None);

        Assert.Equal(1, contexto.Prompt.AvisosEncerramento);
        Assert.Equal(1, contexto.Prompt.AvisosFechados);
        Assert.Equal(0, contexto.Sessao.Encerramentos);
    }

    private static Contexto Criar(
        ModoDeteccaoReuniao modo,
        SinaisDeteccaoReuniao? sinais = null,
        bool finalizacaoAutomatica = false)
    {
        var relogio = new RelogioControlado();
        var fonte = new FonteFake(sinais ?? new SinaisDeteccaoReuniao(true, true, Meet, false, false));
        var politica = new PoliticaDeteccaoReuniao(
            PoliticaDeteccaoOptions.Padrao with
            {
                Modo = modo,
                FinalizacaoAutomaticaAtiva = finalizacaoAutomatica
            });
        var detector = new DetectorReuniao(fonte, politica, relogio);
        var sessao = new SessaoFake();
        var prompt = new PromptFake();
        return new Contexto(
            new CapturaInstantaneaController(detector, sessao, prompt),
            detector,
            fonte,
            sessao,
            prompt,
            relogio);
    }

    private sealed record Contexto(
        CapturaInstantaneaController Controller,
        DetectorReuniao Detector,
        FonteFake Fonte,
        SessaoFake Sessao,
        PromptFake Prompt,
        RelogioControlado Relogio);

    private sealed class FonteFake(SinaisDeteccaoReuniao sinais) : ISinaisReuniaoSource
    {
        public int Leituras { get; private set; }

        public SinaisDeteccaoReuniao Sinais { get; set; } = sinais;

        public Task<SinaisDeteccaoReuniao> ObterAsync(CancellationToken cancellationToken)
        {
            Leituras++;
            return Task.FromResult(Sinais);
        }
    }

    private sealed class RelogioControlado : TimeProvider
    {
        private DateTimeOffset _agora =
            new(2026, 8, 5, 18, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan duracao) => _agora = _agora.Add(duracao);
    }

    private sealed class PromptFake : IDeteccaoPrompt
    {
        public AcaoSugestaoDeteccao ProximaAcao { get; set; } = AcaoSugestaoDeteccao.Fechar;
        public bool CancelamentoInicioPendente { get; set; }
        public bool CancelamentoEncerramentoPendente { get; set; }
        public int Sugestoes { get; private set; }
        public int ContagensExibidas { get; private set; }
        public int ContagensFechadas { get; private set; }
        public int IniciosNotificados { get; private set; }
        public int AvisosEncerramento { get; private set; }
        public int AvisosFechados { get; private set; }
        public int AcoesFechadas { get; private set; }

        public Task<AcaoSugestaoDeteccao> SugerirInicioAsync(
            PlataformaLocal plataforma,
            CancellationToken cancellationToken)
        {
            Sugestoes++;
            return Task.FromResult(ProximaAcao);
        }

        public bool ConsumirCancelamentoInicio()
        {
            var valor = CancelamentoInicioPendente;
            CancelamentoInicioPendente = false;
            return valor;
        }

        public void MostrarContagemInicio(PlataformaLocal plataforma, TimeSpan restante) =>
            ContagensExibidas++;

        public void FecharContagemInicio() => ContagensFechadas++;

        public bool ConsumirCancelamentoEncerramento()
        {
            var valor = CancelamentoEncerramentoPendente;
            CancelamentoEncerramentoPendente = false;
            return valor;
        }

        public void MostrarAvisoEncerramento(TimeSpan restante) => AvisosEncerramento++;

        public void FecharAvisoEncerramento()
        {
            AvisosFechados++;
        }

        public void NotificarInicioAutomatico() => IniciosNotificados++;

        public void MostrarFalhaSegura()
        {
        }

        public void Fechar() => AcoesFechadas++;

        public void Dispose()
        {
        }
    }

    private sealed class SessaoFake : IDesktopSession
    {
        public bool ModoDemonstracao => false;
        public bool Inicializada { get; set; } = true;
        public EtapaDesktopPoc Etapa { get; set; } = EtapaDesktopPoc.Pronto;
        public Guid? ReuniaoAtivaId { get; set; }
        public TimeSpan DuracaoGravacao => TimeSpan.Zero;
        public IReadOnlyList<ReuniaoDesktopPoc> Reunioes => [];
        public bool RecuperacaoPendente { get; set; }
        public int Inicios { get; private set; }
        public int Encerramentos { get; private set; }
        public bool FalharEncerramento { get; set; }
        public string? UltimoTitulo { get; private set; }

        public Task AtualizarAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task IniciarGravacaoAsync(string titulo, CancellationToken cancellationToken)
        {
            Inicios++;
            UltimoTitulo = titulo;
            Etapa = EtapaDesktopPoc.Gravando;
            ReuniaoAtivaId = Guid.NewGuid();
            return Task.CompletedTask;
        }

        public void AvancarGravacao()
        {
        }

        public Task EncerrarGravacaoAsync(CancellationToken cancellationToken)
        {
            Encerramentos++;
            if (FalharEncerramento)
            {
                throw new InvalidOperationException("OBS temporariamente indisponível.");
            }

            Etapa = EtapaDesktopPoc.Processando;
            ReuniaoAtivaId = null;
            return Task.CompletedTask;
        }

        public void ConcluirProcessamentoSimulado()
        {
        }

        public Task<ReuniaoDesktopPoc?> ObterDetalheAsync(
            Guid reuniaoId,
            CancellationToken cancellationToken) => Task.FromResult<ReuniaoDesktopPoc?>(null);

        public Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
