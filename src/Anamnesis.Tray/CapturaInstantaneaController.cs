using Anamnesis.Application.Modelos;
using Anamnesis.Application.UseCases;

namespace Anamnesis.Tray;

internal sealed class CapturaInstantaneaController(
    DetectorReuniao detector,
    IDesktopSession sessao,
    IDeteccaoPrompt prompt) : IDisposable
{
    private int _processando;

    public event Action<GravacaoAutomaticaInfo>? GravacaoAutomaticaIniciada;

    public event Action? GravacaoAutomaticaEncerrada;

    public event Action? FalhaDeteccao;

    public async Task ProcessarAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _processando, 1) != 0)
        {
            return;
        }

        try
        {
            if (!sessao.Inicializada || sessao.RecuperacaoPendente)
            {
                prompt.FecharContagemInicio();
                prompt.FecharAvisoEncerramento();
                return;
            }

            if (prompt.ConsumirCancelamentoInicio())
            {
                await detector.CancelarContagemAsync(cancellationToken);
                prompt.FecharContagemInicio();
                return;
            }

            if (prompt.ConsumirCancelamentoEncerramento())
            {
                await detector.CancelarEncerramentoAsync(cancellationToken);
                prompt.FecharAvisoEncerramento();
                return;
            }

            var decisao = await detector.AvaliarAsync(
                sessao.ReuniaoAtivaId,
                cancellationToken);
            await AplicarDecisaoAsync(decisao, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await sessao.RegistrarFalhaOperacionalAsync(
                "processar_deteccao",
                exception,
                CancellationToken.None);
            await PublicarFalhaDeteccaoAsync();
            prompt.MostrarFalhaSegura();
        }
        finally
        {
            Volatile.Write(ref _processando, 0);
        }
    }

    public async Task SilenciarAsync(CancellationToken cancellationToken)
    {
        await detector.SilenciarAsync(cancellationToken);
        prompt.Fechar();
    }

    public void Dispose() => prompt.Dispose();

    private async Task AplicarDecisaoAsync(
        DecisaoDeteccaoReuniao decisao,
        CancellationToken cancellationToken)
    {
        switch (decisao.Tipo)
        {
            case TipoDecisaoDeteccao.SugerirInicio when decisao.Plataforma is not null:
                await AplicarSugestaoAsync(decisao.Plataforma, cancellationToken);
                break;
            case TipoDecisaoDeteccao.IniciarContagem or
                TipoDecisaoDeteccao.AtualizarContagem when decisao.Plataforma is not null:
                prompt.MostrarContagemInicio(
                    decisao.Plataforma,
                    decisao.Restante ?? TimeSpan.Zero);
                break;
            case TipoDecisaoDeteccao.CancelarContagem:
                prompt.FecharContagemInicio();
                break;
            case TipoDecisaoDeteccao.IniciarGravacao when decisao.Plataforma is not null:
                prompt.FecharContagemInicio();
                await IniciarAsync(decisao.Plataforma, automatico: true, cancellationToken);
                break;
            case TipoDecisaoDeteccao.AvisarEncerramento or
                TipoDecisaoDeteccao.AtualizarAvisoEncerramento:
                prompt.MostrarAvisoEncerramento(decisao.Restante ?? TimeSpan.Zero);
                break;
            case TipoDecisaoDeteccao.CancelarAvisoEncerramento:
                prompt.FecharAvisoEncerramento();
                break;
            case TipoDecisaoDeteccao.EncerrarGravacao:
                prompt.FecharAvisoEncerramento();
                await EncerrarAsync(cancellationToken);
                break;
        }
    }

    private async Task AplicarSugestaoAsync(
        PlataformaLocal plataforma,
        CancellationToken cancellationToken)
    {
        var acao = await prompt.SugerirInicioAsync(plataforma, cancellationToken);
        switch (acao)
        {
            case AcaoSugestaoDeteccao.Iniciar:
                await IniciarAsync(plataforma, automatico: false, cancellationToken);
                break;
            case AcaoSugestaoDeteccao.Ignorar:
                await detector.IgnorarAsync(cancellationToken);
                break;
            case AcaoSugestaoDeteccao.Silenciar:
                await detector.SilenciarAsync(cancellationToken);
                break;
        }
    }

    private async Task IniciarAsync(
        PlataformaLocal plataforma,
        bool automatico,
        CancellationToken cancellationToken)
    {
        if (sessao.Etapa == EtapaDesktopPoc.Gravando || sessao.RecuperacaoPendente)
        {
            return;
        }

        try
        {
            await sessao.IniciarGravacaoAsync(
                $"Reunião detectada • {plataforma.Nome}",
                cancellationToken);
            if (automatico)
            {
                var reuniaoId = sessao.ReuniaoAtivaId
                    ?? throw new InvalidOperationException(
                        "A gravação iniciada não possui identificador ativo.");
                detector.ConfirmarInicioAutomatico(reuniaoId);
                await PublicarInicioAutomaticoAsync(
                    new GravacaoAutomaticaInfo(
                        reuniaoId,
                        $"Reunião detectada • {plataforma.Nome}",
                        plataforma.Nome));
                prompt.NotificarInicioAutomatico();
            }
        }
        catch (Exception exception)
        {
            await detector.RegistrarFalhaInicioAsync(plataforma, CancellationToken.None);
            await sessao.RegistrarFalhaOperacionalAsync(
                "iniciar_gravacao_detectada",
                exception,
                CancellationToken.None);
            await PublicarFalhaDeteccaoAsync();
            prompt.MostrarFalhaSegura();
        }
    }

    private async Task EncerrarAsync(CancellationToken cancellationToken)
    {
        if (sessao.Etapa != EtapaDesktopPoc.Gravando)
        {
            return;
        }

        try
        {
            await sessao.EncerrarGravacaoAsync(cancellationToken);
            await PublicarEncerramentoAutomaticoAsync();
        }
        catch (Exception exception)
        {
            await detector.CancelarEncerramentoAsync(CancellationToken.None);
            await sessao.RegistrarFalhaOperacionalAsync(
                "encerrar_gravacao_detectada",
                exception,
                CancellationToken.None);
            await PublicarFalhaDeteccaoAsync();
            prompt.MostrarFalhaSegura();
        }
    }

    private async Task PublicarInicioAutomaticoAsync(GravacaoAutomaticaInfo info)
    {
        if (GravacaoAutomaticaIniciada is null)
        {
            return;
        }

        foreach (Action<GravacaoAutomaticaInfo> observador in
                 GravacaoAutomaticaIniciada.GetInvocationList())
        {
            try
            {
                observador(info);
            }
            catch (Exception exception)
            {
                await RegistrarFalhaVisualAsync(
                    "exibir_indicador_gravacao_automatica",
                    exception);
            }
        }
    }

    private async Task PublicarEncerramentoAutomaticoAsync()
    {
        if (GravacaoAutomaticaEncerrada is null)
        {
            return;
        }

        foreach (Action observador in GravacaoAutomaticaEncerrada.GetInvocationList())
        {
            try
            {
                observador();
            }
            catch (Exception exception)
            {
                await RegistrarFalhaVisualAsync(
                    "ocultar_indicador_gravacao_automatica",
                    exception);
            }
        }
    }

    private async Task PublicarFalhaDeteccaoAsync()
    {
        if (FalhaDeteccao is null)
        {
            return;
        }

        foreach (Action observador in FalhaDeteccao.GetInvocationList())
        {
            try
            {
                observador();
            }
            catch (Exception exception)
            {
                await RegistrarFalhaVisualAsync(
                    "abrir_contexto_falha_deteccao",
                    exception);
            }
        }
    }

    private async Task RegistrarFalhaVisualAsync(string operacao, Exception exception)
    {
        try
        {
            await sessao.RegistrarFalhaOperacionalAsync(
                operacao,
                exception,
                CancellationToken.None);
        }
        catch (Exception)
        {
            // Presença visual é best-effort e nunca altera o resultado da captura.
        }
    }
}
