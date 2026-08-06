using Anamnesis.Application.Modelos;
using Anamnesis.Application.UseCases;
using Xunit;

namespace Anamnesis.Application.Tests;

public sealed class PoliticaDeteccaoReuniaoTests
{
    private static readonly PlataformaLocal Meet = new(
        "browser_meet",
        "Google Meet",
        OrigemPlataformaLocal.Navegador);

    private static readonly PlataformaLocal Teams = new(
        "native_teams",
        "Microsoft Teams",
        OrigemPlataformaLocal.AplicativoNativo);

    private static readonly DateTimeOffset Agora =
        new(2026, 8, 5, 18, 0, 0, TimeSpan.Zero);

    private static readonly Guid ReuniaoAutomatica =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Guid ReuniaoManual =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void AssistidoDeveSugerirUmaVezENuncaIniciarSemClique()
    {
        var politica = Criar(ModoDeteccaoReuniao.Assistido);
        var sinais = Chamada(Meet);

        var primeira = politica.Avaliar(sinais, Agora, reuniaoAtivaId: null);
        var depoisDeUmMinuto = politica.Avaliar(
            sinais,
            Agora.AddMinutes(1),
            reuniaoAtivaId: null);

        Assert.Equal(TipoDecisaoDeteccao.SugerirInicio, primeira.Tipo);
        Assert.Equal(Meet, primeira.Plataforma);
        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, depoisDeUmMinuto.Tipo);
    }

    [Theory]
    [MemberData(nameof(SinaisInsuficientes))]
    public void AutomaticoDeveExigirMicrofonePlataformaEUmUnicoCandidato(
        SinaisDeteccaoReuniao sinais)
    {
        var politica = Criar(ModoDeteccaoReuniao.Automatico);

        politica.Avaliar(sinais, Agora, reuniaoAtivaId: null);
        var decisao = politica.Avaliar(
            sinais,
            Agora.AddMinutes(1),
            reuniaoAtivaId: null);

        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, decisao.Tipo);
    }

    public static TheoryData<SinaisDeteccaoReuniao> SinaisInsuficientes => new()
    {
        new SinaisDeteccaoReuniao(false, true, Meet, false, false),
        new SinaisDeteccaoReuniao(true, false, null, false, false),
        new SinaisDeteccaoReuniao(false, true, null, true, false),
        new SinaisDeteccaoReuniao(true, true, Meet, false, true)
    };

    [Fact]
    public void AutomaticoDeveSustentarCincoSegundosEContarMaisCinco()
    {
        var politica = Criar(ModoDeteccaoReuniao.Automatico);
        var sinais = Chamada(Meet);

        var inicial = politica.Avaliar(sinais, Agora, reuniaoAtivaId: null);
        var antesDaSustentacao = politica.Avaliar(
            sinais,
            Agora.AddMilliseconds(4_999),
            reuniaoAtivaId: null);
        var contagem = politica.Avaliar(
            sinais,
            Agora.AddSeconds(5),
            reuniaoAtivaId: null);
        var restante = politica.Avaliar(
            sinais,
            Agora.AddSeconds(9),
            reuniaoAtivaId: null);
        var iniciar = politica.Avaliar(
            sinais,
            Agora.AddSeconds(10),
            reuniaoAtivaId: null);

        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, inicial.Tipo);
        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, antesDaSustentacao.Tipo);
        Assert.Equal(TipoDecisaoDeteccao.IniciarContagem, contagem.Tipo);
        Assert.Equal(TimeSpan.FromSeconds(5), contagem.Restante);
        Assert.Equal(TipoDecisaoDeteccao.AtualizarContagem, restante.Tipo);
        Assert.Equal(TimeSpan.FromSeconds(1), restante.Restante);
        Assert.Equal(TipoDecisaoDeteccao.IniciarGravacao, iniciar.Tipo);
        Assert.Equal("microfone_plataforma", iniciar.MotivoCodigo);
    }

    [Fact]
    public void PerderSinalDeveCancelarContagem()
    {
        var politica = Criar(ModoDeteccaoReuniao.Automatico);

        politica.Avaliar(Chamada(Meet), Agora, reuniaoAtivaId: null);
        politica.Avaliar(Chamada(Meet), Agora.AddSeconds(5), reuniaoAtivaId: null);
        var cancelada = politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum,
            Agora.AddSeconds(6),
            reuniaoAtivaId: null);

        Assert.Equal(TipoDecisaoDeteccao.CancelarContagem, cancelada.Tipo);
        Assert.Equal("sinais_perdidos", cancelada.MotivoCodigo);
    }

    [Fact]
    public void FalhaDeColetaDuranteContagemDeveFechaLa()
    {
        var politica = Criar(ModoDeteccaoReuniao.Automatico);

        politica.Avaliar(Chamada(Meet), Agora, reuniaoAtivaId: null);
        politica.Avaliar(Chamada(Meet), Agora.AddSeconds(5), reuniaoAtivaId: null);
        var cancelada = politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum with { ColetaConfiavel = false },
            Agora.AddSeconds(6),
            reuniaoAtivaId: null);

        Assert.Equal(TipoDecisaoDeteccao.CancelarContagem, cancelada.Tipo);
        Assert.Equal("coleta_indisponivel", cancelada.MotivoCodigo);
    }

    [Fact]
    public void CancelarDeveAplicarCooldownSomenteNaMesmaPlataforma()
    {
        var politica = Criar(ModoDeteccaoReuniao.Automatico);

        politica.Avaliar(Chamada(Meet), Agora, reuniaoAtivaId: null);
        politica.Avaliar(Chamada(Meet), Agora.AddSeconds(5), reuniaoAtivaId: null);
        politica.CancelarContagem(Agora.AddSeconds(6));

        politica.Avaliar(Chamada(Meet), Agora.AddSeconds(20), reuniaoAtivaId: null);
        var meetBloqueado = politica.Avaliar(
            Chamada(Meet),
            Agora.AddMinutes(1),
            reuniaoAtivaId: null);
        politica.Avaliar(Chamada(Teams), Agora.AddSeconds(20), reuniaoAtivaId: null);
        var teamsLiberado = politica.Avaliar(
            Chamada(Teams),
            Agora.AddSeconds(25),
            reuniaoAtivaId: null);

        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, meetBloqueado.Tipo);
        Assert.Equal(TipoDecisaoDeteccao.IniciarContagem, teamsLiberado.Tipo);
    }

    [Fact]
    public void SilenciarDevePausarSugestoesSemAlterarModoManual()
    {
        var politica = Criar(ModoDeteccaoReuniao.Assistido);

        politica.Silenciar(Agora);
        var duranteSilencio = politica.Avaliar(
            Chamada(Meet),
            Agora.AddMinutes(30),
            reuniaoAtivaId: null);
        var depoisDoSilencio = politica.Avaliar(
            Chamada(Meet),
            Agora.AddHours(1),
            reuniaoAtivaId: null);

        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, duranteSilencio.Tipo);
        Assert.Equal(TipoDecisaoDeteccao.SugerirInicio, depoisDoSilencio.Tipo);
    }

    [Fact]
    public void GravacaoAtivaDeveBloquearSegundoInicio()
    {
        var politica = Criar(ModoDeteccaoReuniao.Automatico);

        politica.Avaliar(Chamada(Meet), Agora, ReuniaoManual);
        var decisao = politica.Avaliar(
            Chamada(Meet),
            Agora.AddMinutes(1),
            ReuniaoManual);

        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, decisao.Tipo);
    }

    [Fact]
    public void EncerramentoAutomaticoDeveExigirInicioAutomaticoEAvisoCancelavel()
    {
        var options = PoliticaDeteccaoOptions.Padrao with
        {
            Modo = ModoDeteccaoReuniao.Automatico,
            FinalizacaoAutomaticaAtiva = true
        };
        var politica = new PoliticaDeteccaoReuniao(options);
        politica.ConfirmarInicioAutomatico(ReuniaoAutomatica);

        var inicial = politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum,
            Agora,
            ReuniaoAutomatica);
        var aviso = politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum,
            Agora.AddMinutes(2),
            ReuniaoAutomatica);
        var encerrar = politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum,
            Agora.AddMinutes(2).AddSeconds(15),
            ReuniaoAutomatica);

        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, inicial.Tipo);
        Assert.Equal(TipoDecisaoDeteccao.AvisarEncerramento, aviso.Tipo);
        Assert.Equal(TimeSpan.FromSeconds(15), aviso.Restante);
        Assert.Equal(TipoDecisaoDeteccao.EncerrarGravacao, encerrar.Tipo);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RetornoDeSinaisOuFalhaDeColetaDeveFecharAvisoDeEncerramento(
        bool coletaConfiavel)
    {
        var options = PoliticaDeteccaoOptions.Padrao with
        {
            Modo = ModoDeteccaoReuniao.Automatico,
            FinalizacaoAutomaticaAtiva = true
        };
        var politica = new PoliticaDeteccaoReuniao(options);
        politica.ConfirmarInicioAutomatico(ReuniaoAutomatica);
        politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum,
            Agora,
            ReuniaoAutomatica);
        politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum,
            Agora.AddMinutes(2),
            ReuniaoAutomatica);
        var sinais = coletaConfiavel
            ? Chamada(Meet)
            : SinaisDeteccaoReuniao.Nenhum with { ColetaConfiavel = false };

        var cancelada = politica.Avaliar(
            sinais,
            Agora.AddMinutes(2).AddSeconds(1),
            ReuniaoAutomatica);

        Assert.Equal(TipoDecisaoDeteccao.CancelarAvisoEncerramento, cancelada.Tipo);
    }

    [Fact]
    public void PosseDoAutoStopDeveSerDaReuniaoAutomaticaAtual()
    {
        var options = PoliticaDeteccaoOptions.Padrao with
        {
            Modo = ModoDeteccaoReuniao.Automatico,
            FinalizacaoAutomaticaAtiva = true
        };
        var politica = new PoliticaDeteccaoReuniao(options);
        politica.ConfirmarInicioAutomatico(ReuniaoAutomatica);
        politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum,
            Agora,
            ReuniaoAutomatica);

        var reuniaoManual = politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum,
            Agora.AddMinutes(10),
            ReuniaoManual);
        var depois = politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum,
            Agora.AddMinutes(20),
            ReuniaoManual);

        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, reuniaoManual.Tipo);
        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, depois.Tipo);
    }

    [Fact]
    public void FalhaDeColetaNuncaDeveContarComoAusenciaParaEncerramento()
    {
        var options = PoliticaDeteccaoOptions.Padrao with
        {
            Modo = ModoDeteccaoReuniao.Automatico,
            FinalizacaoAutomaticaAtiva = true
        };
        var politica = new PoliticaDeteccaoReuniao(options);
        politica.ConfirmarInicioAutomatico(ReuniaoAutomatica);
        var coletaIndisponivel = SinaisDeteccaoReuniao.Nenhum with
        {
            ColetaConfiavel = false
        };

        politica.Avaliar(coletaIndisponivel, Agora, ReuniaoAutomatica);
        var depoisDeDezMinutos = politica.Avaliar(
            coletaIndisponivel,
            Agora.AddMinutes(10),
            ReuniaoAutomatica);
        var primeiraAusenciaConfiavel = politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum,
            Agora.AddMinutes(11),
            ReuniaoAutomatica);

        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, depoisDeDezMinutos.Tipo);
        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, primeiraAusenciaConfiavel.Tipo);
    }

    [Fact]
    public void ReinicioOuInicioManualNuncaDeveEncerrarAutomaticamente()
    {
        var options = PoliticaDeteccaoOptions.Padrao with
        {
            Modo = ModoDeteccaoReuniao.Automatico,
            FinalizacaoAutomaticaAtiva = true
        };
        var politica = new PoliticaDeteccaoReuniao(options);

        politica.Avaliar(SinaisDeteccaoReuniao.Nenhum, Agora, ReuniaoManual);
        var decisao = politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum,
            Agora.AddMinutes(10),
            ReuniaoManual);

        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, decisao.Tipo);
    }

    [Fact]
    public void CancelarEncerramentoDeveAguardarRetornoDosSinaisAntesDeRearmar()
    {
        var options = PoliticaDeteccaoOptions.Padrao with
        {
            Modo = ModoDeteccaoReuniao.Automatico,
            FinalizacaoAutomaticaAtiva = true
        };
        var politica = new PoliticaDeteccaoReuniao(options);
        politica.ConfirmarInicioAutomatico(ReuniaoAutomatica);
        politica.Avaliar(SinaisDeteccaoReuniao.Nenhum, Agora, ReuniaoAutomatica);
        politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum,
            Agora.AddMinutes(2),
            ReuniaoAutomatica);

        politica.CancelarEncerramento();
        var aindaAusente = politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum,
            Agora.AddMinutes(10),
            ReuniaoAutomatica);
        politica.Avaliar(Chamada(Meet), Agora.AddMinutes(11), ReuniaoAutomatica);
        var novaAusencia = politica.Avaliar(
            SinaisDeteccaoReuniao.Nenhum,
            Agora.AddMinutes(12),
            ReuniaoAutomatica);

        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, aindaAusente.Tipo);
        Assert.Equal(TipoDecisaoDeteccao.Nenhuma, novaAusencia.Tipo);
    }

    private static PoliticaDeteccaoReuniao Criar(ModoDeteccaoReuniao modo) =>
        new(PoliticaDeteccaoOptions.Padrao with { Modo = modo });

    private static SinaisDeteccaoReuniao Chamada(PlataformaLocal plataforma) =>
        new(
            MicrofoneAtivo: true,
            AudioSaidaAtivo: true,
            Plataforma: plataforma,
            EventoAgendaProximo: false,
            Ambiguo: false);
}
