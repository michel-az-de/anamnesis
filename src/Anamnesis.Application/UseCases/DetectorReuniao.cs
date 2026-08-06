using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;

namespace Anamnesis.Application.UseCases;

public sealed class DetectorReuniao(
    ISinaisReuniaoSource fonte,
    PoliticaDeteccaoReuniao politica,
    TimeProvider relogio,
    JornalOperacional? journal = null)
{
    private readonly JornalOperacional _journal = journal ?? JornalOperacional.Nulo;
    private bool _coletaConfiavelAnterior = true;

    public async Task<DecisaoDeteccaoReuniao> AvaliarAsync(
        Guid? reuniaoAtivaId,
        CancellationToken cancellationToken)
    {
        SinaisDeteccaoReuniao sinais;
        try
        {
            sinais = await fonte.ObterAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await _journal.RegistrarFalhaAsync(
                "Detector",
                "coletar_sinais",
                exception,
                null,
                null,
                CancellationToken.None);
            return await AvaliarPoliticaAsync(
                SinaisDeteccaoReuniao.Nenhum with { ColetaConfiavel = false },
                reuniaoAtivaId,
                cancellationToken);
        }

        return await AvaliarPoliticaAsync(sinais, reuniaoAtivaId, cancellationToken);
    }

    private async Task<DecisaoDeteccaoReuniao> AvaliarPoliticaAsync(
        SinaisDeteccaoReuniao sinais,
        Guid? reuniaoAtivaId,
        CancellationToken cancellationToken)
    {
        await RegistrarTransicaoSaudeAsync(sinais.ColetaConfiavel, cancellationToken);
        var decisao = politica.Avaliar(sinais, relogio.GetUtcNow(), reuniaoAtivaId);
        if (DeveRegistrar(decisao.Tipo))
        {
            await RegistrarDecisaoAsync(decisao, cancellationToken);
        }

        return decisao;
    }

    private async Task RegistrarTransicaoSaudeAsync(
        bool coletaConfiavel,
        CancellationToken cancellationToken)
    {
        if (_coletaConfiavelAnterior == coletaConfiavel)
        {
            return;
        }

        _coletaConfiavelAnterior = coletaConfiavel;
        await _journal.RegistrarAsync(
            NivelEventoOperacional.Info,
            CodigosEventoOperacional.DeteccaoDecidida,
            "Detector",
            coletaConfiavel
                ? "Coleta local de sinais restaurada."
                : "Coleta local de sinais degradada.",
            null,
            null,
            new MetadadosEventoOperacional(
                Operacao: "avaliar_saude_coleta",
                Resultado: coletaConfiavel ? "restaurada" : "degradada",
                MotivoCodigo: coletaConfiavel
                    ? "probes_disponiveis"
                    : "probe_indisponivel"),
            cancellationToken);
    }

    public async Task IgnorarAsync(CancellationToken cancellationToken)
    {
        politica.Ignorar(relogio.GetUtcNow());
        await RegistrarAcaoUsuarioAsync("ignorar", "usuario_ignorou", cancellationToken);
    }

    public async Task CancelarContagemAsync(CancellationToken cancellationToken)
    {
        politica.CancelarContagem(relogio.GetUtcNow());
        await RegistrarAcaoUsuarioAsync("cancelar_contagem", "usuario_cancelou", cancellationToken);
    }

    public async Task SilenciarAsync(CancellationToken cancellationToken)
    {
        politica.Silenciar(relogio.GetUtcNow());
        await RegistrarAcaoUsuarioAsync("silenciar", "usuario_silenciou", cancellationToken);
    }

    public void ConfirmarInicioAutomatico(Guid reuniaoId) =>
        politica.ConfirmarInicioAutomatico(reuniaoId);

    public async Task RegistrarFalhaInicioAsync(
        PlataformaLocal plataforma,
        CancellationToken cancellationToken)
    {
        politica.RegistrarFalhaInicio(plataforma, relogio.GetUtcNow());
        await RegistrarAcaoUsuarioAsync(
            "inicio_falhou",
            "cooldown_apos_falha",
            cancellationToken);
    }

    public async Task CancelarEncerramentoAsync(CancellationToken cancellationToken)
    {
        politica.CancelarEncerramento();
        await RegistrarAcaoUsuarioAsync(
            "cancelar_encerramento",
            "usuario_manteve_gravacao",
            cancellationToken);
    }

    private Task RegistrarDecisaoAsync(
        DecisaoDeteccaoReuniao decisao,
        CancellationToken cancellationToken) =>
        _journal.RegistrarAsync(
            NivelEventoOperacional.Info,
            CodigosEventoOperacional.DeteccaoDecidida,
            "Detector",
            MensagemSegura(decisao.Tipo),
            null,
            null,
            new MetadadosEventoOperacional(
                Operacao: "avaliar_deteccao",
                Resultado: CodigoResultado(decisao.Tipo),
                MotivoCodigo: decisao.MotivoCodigo),
            cancellationToken);

    private Task RegistrarAcaoUsuarioAsync(
        string resultado,
        string motivo,
        CancellationToken cancellationToken) =>
        _journal.RegistrarAsync(
            NivelEventoOperacional.Info,
            CodigosEventoOperacional.DeteccaoDecidida,
            "Detector",
            "Preferência local de detecção aplicada.",
            null,
            null,
            new MetadadosEventoOperacional(
                Operacao: "configurar_deteccao",
                Resultado: resultado,
                MotivoCodigo: motivo),
            cancellationToken);

    private static bool DeveRegistrar(TipoDecisaoDeteccao tipo) =>
        tipo is not TipoDecisaoDeteccao.Nenhuma and
            not TipoDecisaoDeteccao.AtualizarContagem and
            not TipoDecisaoDeteccao.AtualizarAvisoEncerramento;

    private static string CodigoResultado(TipoDecisaoDeteccao tipo) => tipo switch
    {
        TipoDecisaoDeteccao.SugerirInicio => "sugerir_inicio",
        TipoDecisaoDeteccao.IniciarContagem => "iniciar_contagem",
        TipoDecisaoDeteccao.CancelarContagem => "cancelar_contagem",
        TipoDecisaoDeteccao.IniciarGravacao => "iniciar_gravacao",
        TipoDecisaoDeteccao.AvisarEncerramento => "avisar_encerramento",
        TipoDecisaoDeteccao.CancelarAvisoEncerramento => "cancelar_aviso_encerramento",
        TipoDecisaoDeteccao.EncerrarGravacao => "encerrar_gravacao",
        _ => "sem_acao"
    };

    private static string MensagemSegura(TipoDecisaoDeteccao tipo) => tipo switch
    {
        TipoDecisaoDeteccao.SugerirInicio => "Sugestão de captura disponível.",
        TipoDecisaoDeteccao.IniciarContagem => "Contagem regressiva de captura iniciada.",
        TipoDecisaoDeteccao.CancelarContagem => "Contagem regressiva de captura cancelada.",
        TipoDecisaoDeteccao.IniciarGravacao => "Início automático de captura solicitado.",
        TipoDecisaoDeteccao.AvisarEncerramento => "Aviso de encerramento automático iniciado.",
        TipoDecisaoDeteccao.CancelarAvisoEncerramento => "Aviso de encerramento automático cancelado.",
        TipoDecisaoDeteccao.EncerrarGravacao => "Encerramento automático solicitado.",
        _ => "Decisão local de captura avaliada."
    };
}
