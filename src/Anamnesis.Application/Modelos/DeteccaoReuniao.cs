namespace Anamnesis.Application.Modelos;

public enum ModoDeteccaoReuniao
{
    Manual,
    Assistido,
    Automatico
}

public enum OrigemPlataformaLocal
{
    AplicativoNativo,
    Navegador
}

public sealed record PlataformaLocal(
    string Chave,
    string Nome,
    OrigemPlataformaLocal Origem);

public sealed record SinaisDeteccaoReuniao(
    bool MicrofoneAtivo,
    bool AudioSaidaAtivo,
    PlataformaLocal? Plataforma,
    bool EventoAgendaProximo,
    bool Ambiguo,
    bool ColetaConfiavel = true)
{
    public static SinaisDeteccaoReuniao Nenhum { get; } =
        new(false, false, null, false, false);

    public bool PossuiAtividade =>
        MicrofoneAtivo || AudioSaidaAtivo || Plataforma is not null || EventoAgendaProximo;
}

public enum TipoDecisaoDeteccao
{
    Nenhuma,
    SugerirInicio,
    IniciarContagem,
    AtualizarContagem,
    CancelarContagem,
    IniciarGravacao,
    AvisarEncerramento,
    AtualizarAvisoEncerramento,
    CancelarAvisoEncerramento,
    EncerrarGravacao
}

public sealed record DecisaoDeteccaoReuniao(
    TipoDecisaoDeteccao Tipo,
    string MotivoCodigo,
    PlataformaLocal? Plataforma = null,
    TimeSpan? Restante = null)
{
    public static DecisaoDeteccaoReuniao Nenhuma { get; } =
        new(TipoDecisaoDeteccao.Nenhuma, "sem_acao");
}

public sealed record PoliticaDeteccaoOptions
{
    public ModoDeteccaoReuniao Modo { get; init; } = ModoDeteccaoReuniao.Assistido;

    public TimeSpan Sustentacao { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan ContagemRegressiva { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan Cooldown { get; init; } = TimeSpan.FromMinutes(10);

    public TimeSpan DuracaoSilencio { get; init; } = TimeSpan.FromHours(1);

    public bool FinalizacaoAutomaticaAtiva { get; init; }

    public TimeSpan AusenciaParaEncerrar { get; init; } = TimeSpan.FromMinutes(2);

    public TimeSpan AvisoEncerramento { get; init; } = TimeSpan.FromSeconds(15);

    public static PoliticaDeteccaoOptions Padrao { get; } = new();
}
