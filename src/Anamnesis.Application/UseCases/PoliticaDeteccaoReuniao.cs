using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.UseCases;

public sealed class PoliticaDeteccaoReuniao
{
    private readonly PoliticaDeteccaoOptions _options;
    private readonly Dictionary<string, DateTimeOffset> _cooldowns = new(StringComparer.Ordinal);
    private string? _chaveAtual;
    private PlataformaLocal? _plataformaAtual;
    private DateTimeOffset? _sustentacaoDesde;
    private DateTimeOffset? _contagemDesde;
    private DateTimeOffset? _silenciadoAte;
    private bool _sugestaoEmitida;
    private Guid? _reuniaoAutomaticaId;
    private DateTimeOffset? _ausenciaDesde;
    private DateTimeOffset? _avisoEncerramentoDesde;
    private bool _encerramentoSuprimido;

    public PoliticaDeteccaoReuniao(PoliticaDeteccaoOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidarDuracao(options.Sustentacao, nameof(options.Sustentacao));
        ValidarDuracao(options.ContagemRegressiva, nameof(options.ContagemRegressiva));
        ValidarDuracao(options.Cooldown, nameof(options.Cooldown));
        ValidarDuracao(options.DuracaoSilencio, nameof(options.DuracaoSilencio));
        ValidarDuracao(options.AusenciaParaEncerrar, nameof(options.AusenciaParaEncerrar));
        ValidarDuracao(options.AvisoEncerramento, nameof(options.AvisoEncerramento));
        _options = options;
    }

    public DecisaoDeteccaoReuniao Avaliar(
        SinaisDeteccaoReuniao sinais,
        DateTimeOffset agora,
        Guid? reuniaoAtivaId)
    {
        ArgumentNullException.ThrowIfNull(sinais);

        if (!sinais.ColetaConfiavel)
        {
            if (_contagemDesde is not null)
            {
                var cancelarContagem = new DecisaoDeteccaoReuniao(
                    TipoDecisaoDeteccao.CancelarContagem,
                    "coleta_indisponivel",
                    _plataformaAtual);
                ReiniciarInicio();
                return cancelarContagem;
            }

            if (_avisoEncerramentoDesde is not null)
            {
                ReiniciarEncerramento(liberarSupressao: false);
                return CancelarAviso("coleta_indisponivel");
            }

            ReiniciarInicio();
            ReiniciarEncerramento(liberarSupressao: false);
            return DecisaoDeteccaoReuniao.Nenhuma;
        }

        if (_silenciadoAte is not null && agora < _silenciadoAte.Value)
        {
            ReiniciarInicio();
            ReiniciarEncerramento(liberarSupressao: false);
            return DecisaoDeteccaoReuniao.Nenhuma;
        }

        if (reuniaoAtivaId is not null)
        {
            ReiniciarInicio();
            return AvaliarEncerramento(sinais, agora, reuniaoAtivaId.Value);
        }

        var avisoPendente = _avisoEncerramentoDesde is not null;
        _reuniaoAutomaticaId = null;
        ReiniciarEncerramento(liberarSupressao: true);
        if (avisoPendente)
        {
            return CancelarAviso("gravacao_inativa");
        }

        if (_options.Modo == ModoDeteccaoReuniao.Manual)
        {
            ReiniciarInicio();
            return DecisaoDeteccaoReuniao.Nenhuma;
        }

        RemoverCooldownsExpirados(agora);
        return _options.Modo == ModoDeteccaoReuniao.Assistido
            ? AvaliarAssistido(sinais, agora)
            : AvaliarAutomatico(sinais, agora);
    }

    public void Ignorar(DateTimeOffset agora) => AplicarCooldown(agora);

    public void CancelarContagem(DateTimeOffset agora) => AplicarCooldown(agora);

    public void Silenciar(DateTimeOffset agora)
    {
        _silenciadoAte = agora.Add(_options.DuracaoSilencio);
        ReiniciarInicio();
    }

    public void ConfirmarInicioAutomatico(Guid reuniaoId)
    {
        if (reuniaoId == Guid.Empty)
        {
            throw new ArgumentException(
                "A reunião automática precisa de um identificador válido.",
                nameof(reuniaoId));
        }

        _reuniaoAutomaticaId = reuniaoId;
        ReiniciarInicio();
    }

    public void RegistrarFalhaInicio(PlataformaLocal plataforma, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(plataforma);
        _cooldowns[plataforma.Chave] = agora.Add(_options.Cooldown);
        ReiniciarInicio();
    }

    public void CancelarEncerramento()
    {
        _encerramentoSuprimido = true;
        _ausenciaDesde = null;
        _avisoEncerramentoDesde = null;
    }

    private DecisaoDeteccaoReuniao AvaliarAssistido(
        SinaisDeteccaoReuniao sinais,
        DateTimeOffset agora)
    {
        var plataforma = sinais.Plataforma;
        var sinalForte = plataforma is not null &&
            !sinais.Ambiguo &&
            (sinais.MicrofoneAtivo || sinais.AudioSaidaAtivo);
        if (!sinalForte || plataforma is null || EstaEmCooldown(plataforma.Chave, agora))
        {
            ReiniciarInicio();
            return DecisaoDeteccaoReuniao.Nenhuma;
        }

        if (!string.Equals(_chaveAtual, plataforma.Chave, StringComparison.Ordinal))
        {
            ReiniciarInicio();
            _chaveAtual = plataforma.Chave;
            _plataformaAtual = plataforma;
        }

        if (_sugestaoEmitida)
        {
            return DecisaoDeteccaoReuniao.Nenhuma;
        }

        _sugestaoEmitida = true;
        return new DecisaoDeteccaoReuniao(
            TipoDecisaoDeteccao.SugerirInicio,
            "plataforma_audio",
            plataforma);
    }

    private DecisaoDeteccaoReuniao AvaliarAutomatico(
        SinaisDeteccaoReuniao sinais,
        DateTimeOffset agora)
    {
        var plataforma = sinais.Plataforma;
        var elegivel = sinais.MicrofoneAtivo && plataforma is not null && !sinais.Ambiguo;
        if (!elegivel || plataforma is null || EstaEmCooldown(plataforma.Chave, agora))
        {
            if (_contagemDesde is not null)
            {
                var cancelada = new DecisaoDeteccaoReuniao(
                    TipoDecisaoDeteccao.CancelarContagem,
                    "sinais_perdidos",
                    _plataformaAtual);
                ReiniciarInicio();
                return cancelada;
            }

            ReiniciarInicio();
            return DecisaoDeteccaoReuniao.Nenhuma;
        }

        if (!string.Equals(_chaveAtual, plataforma.Chave, StringComparison.Ordinal))
        {
            ReiniciarInicio();
            _chaveAtual = plataforma.Chave;
            _plataformaAtual = plataforma;
            _sustentacaoDesde = agora;
            return DecisaoDeteccaoReuniao.Nenhuma;
        }

        _sustentacaoDesde ??= agora;
        if (_contagemDesde is null)
        {
            if (agora - _sustentacaoDesde.Value < _options.Sustentacao)
            {
                return DecisaoDeteccaoReuniao.Nenhuma;
            }

            _contagemDesde = agora;
            return new DecisaoDeteccaoReuniao(
                TipoDecisaoDeteccao.IniciarContagem,
                "sinais_sustentados",
                plataforma,
                _options.ContagemRegressiva);
        }

        var decorrido = agora - _contagemDesde.Value;
        if (decorrido < _options.ContagemRegressiva)
        {
            return new DecisaoDeteccaoReuniao(
                TipoDecisaoDeteccao.AtualizarContagem,
                "contagem_ativa",
                plataforma,
                _options.ContagemRegressiva - decorrido);
        }

        var iniciar = new DecisaoDeteccaoReuniao(
            TipoDecisaoDeteccao.IniciarGravacao,
            "microfone_plataforma",
            plataforma);
        ReiniciarInicio();
        return iniciar;
    }

    private DecisaoDeteccaoReuniao AvaliarEncerramento(
        SinaisDeteccaoReuniao sinais,
        DateTimeOffset agora,
        Guid reuniaoAtivaId)
    {
        if (_reuniaoAutomaticaId != reuniaoAtivaId ||
            !_options.FinalizacaoAutomaticaAtiva)
        {
            var avisoPendente = _avisoEncerramentoDesde is not null;
            if (_reuniaoAutomaticaId != reuniaoAtivaId)
            {
                _reuniaoAutomaticaId = null;
            }

            ReiniciarEncerramento(liberarSupressao: false);
            return avisoPendente
                ? CancelarAviso("gravacao_sem_posse_automatica")
                : DecisaoDeteccaoReuniao.Nenhuma;
        }

        if (sinais.PossuiAtividade)
        {
            var avisoPendente = _avisoEncerramentoDesde is not null;
            ReiniciarEncerramento(liberarSupressao: true);
            return avisoPendente
                ? CancelarAviso("sinais_retornaram")
                : DecisaoDeteccaoReuniao.Nenhuma;
        }

        if (_encerramentoSuprimido)
        {
            return DecisaoDeteccaoReuniao.Nenhuma;
        }

        _ausenciaDesde ??= agora;
        if (agora - _ausenciaDesde.Value < _options.AusenciaParaEncerrar)
        {
            return DecisaoDeteccaoReuniao.Nenhuma;
        }

        if (_avisoEncerramentoDesde is null)
        {
            _avisoEncerramentoDesde = agora;
            return new DecisaoDeteccaoReuniao(
                TipoDecisaoDeteccao.AvisarEncerramento,
                "sinais_ausentes",
                Restante: _options.AvisoEncerramento);
        }

        var decorrido = agora - _avisoEncerramentoDesde.Value;
        if (decorrido < _options.AvisoEncerramento)
        {
            return new DecisaoDeteccaoReuniao(
                TipoDecisaoDeteccao.AtualizarAvisoEncerramento,
                "aviso_encerramento_ativo",
                Restante: _options.AvisoEncerramento - decorrido);
        }

        return new DecisaoDeteccaoReuniao(
            TipoDecisaoDeteccao.EncerrarGravacao,
            "ausencia_confirmada");
    }

    private static DecisaoDeteccaoReuniao CancelarAviso(string motivo) =>
        new(TipoDecisaoDeteccao.CancelarAvisoEncerramento, motivo);

    private void AplicarCooldown(DateTimeOffset agora)
    {
        if (_chaveAtual is not null)
        {
            _cooldowns[_chaveAtual] = agora.Add(_options.Cooldown);
        }

        ReiniciarInicio();
    }

    private bool EstaEmCooldown(string chave, DateTimeOffset agora) =>
        _cooldowns.TryGetValue(chave, out var ate) && agora < ate;

    private void RemoverCooldownsExpirados(DateTimeOffset agora)
    {
        foreach (var chave in _cooldowns
                     .Where(item => item.Value <= agora)
                     .Select(item => item.Key)
                     .ToArray())
        {
            _cooldowns.Remove(chave);
        }
    }

    private void ReiniciarInicio()
    {
        _chaveAtual = null;
        _plataformaAtual = null;
        _sustentacaoDesde = null;
        _contagemDesde = null;
        _sugestaoEmitida = false;
    }

    private void ReiniciarEncerramento(bool liberarSupressao)
    {
        _ausenciaDesde = null;
        _avisoEncerramentoDesde = null;
        if (liberarSupressao)
        {
            _encerramentoSuprimido = false;
        }
    }

    private static void ValidarDuracao(TimeSpan duracao, string nome)
    {
        if (duracao <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nome, "A duracao deve ser maior que zero.");
        }
    }
}
