namespace Anamnesis.Tray;

public enum NivelEventoPoc
{
    Info,
    Aviso,
    Erro
}

public sealed record EventoObservabilidadePoc(
    DateTimeOffset CriadoEm,
    NivelEventoPoc Nivel,
    string Componente,
    string Evento,
    string Mensagem,
    string CorrelacaoId,
    double? DuracaoMs = null);

public sealed record MetricasObservabilidadePoc(
    int TotalEventos,
    int Alertas,
    double DuracaoMediaMs,
    int JobsNaFila);

public sealed class DesktopPocObservabilityState
{
    private readonly Func<DateTimeOffset> _agora;
    private readonly List<EventoObservabilidadePoc> _eventos = [];
    private string? _correlacaoAtual;
    private int _sequenciaCorrelacao;
    private int _jobsNaFila;

    public DesktopPocObservabilityState(
        Func<DateTimeOffset>? agora = null,
        bool incluirHistorico = true)
    {
        _agora = agora ?? (() => DateTimeOffset.Now);
        if (incluirHistorico)
        {
            CriarHistoricoSeguro();
        }
    }

    public IReadOnlyList<EventoObservabilidadePoc> Eventos => _eventos;

    public IReadOnlyList<string> Componentes => _eventos
        .Select(evento => evento.Componente)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(componente => componente, StringComparer.Ordinal)
        .ToArray();

    public MetricasObservabilidadePoc Metricas => CalcularMetricas(_eventos);

    public IReadOnlyList<EventoObservabilidadePoc> Filtrar(
        string? texto = null,
        NivelEventoPoc? nivel = null,
        string? componente = null)
    {
        var filtro = texto?.Trim();
        return _eventos.Where(evento =>
                (nivel is null || evento.Nivel == nivel) &&
                (string.IsNullOrWhiteSpace(componente) || string.Equals(evento.Componente, componente, StringComparison.Ordinal)) &&
                (string.IsNullOrWhiteSpace(filtro) || Contem(evento, filtro)))
            .ToArray();
    }

    public MetricasObservabilidadePoc CalcularMetricas(IEnumerable<EventoObservabilidadePoc> eventos)
    {
        var visiveis = eventos.ToArray();
        var duracoes = visiveis
            .Where(evento => evento.DuracaoMs.HasValue)
            .Select(evento => evento.DuracaoMs!.Value)
            .ToArray();

        return new MetricasObservabilidadePoc(
            visiveis.Length,
            visiveis.Count(evento => evento.Nivel is NivelEventoPoc.Aviso or NivelEventoPoc.Erro),
            duracoes.Length == 0 ? 0 : Math.Round(duracoes.Average(), 1),
            _jobsNaFila);
    }

    public void RegistrarInicioGravacao()
    {
        _sequenciaCorrelacao++;
        _correlacaoAtual = $"sim-reuniao-{_sequenciaCorrelacao:00}";
        Registrar(NivelEventoPoc.Info, "Tray", "gravacao.iniciada", "Sessão de captura iniciada.", 18);
        Registrar(NivelEventoPoc.Info, "OBS", "captura.saudavel", "Áudio do sistema e microfone estão ativos.", 84);
    }

    public void RegistrarFimGravacao(TimeSpan duracao)
    {
        GarantirCorrelacao();
        Registrar(NivelEventoPoc.Info, "OBS", "gravacao.encerrada", $"Captura encerrada após {FormatarDuracao(duracao)}.", 31);
        Registrar(NivelEventoPoc.Info, "Fila", "job.criado", "Job durável criado para processamento local.", 7);
        _jobsNaFila = 1;
        Registrar(NivelEventoPoc.Info, "Worker", "job.reservado", "Worker reservou o job para execução.", 14);
        Registrar(NivelEventoPoc.Info, "Whisper", "transcricao.iniciada", "Transcrição local iniciada.");
    }

    public void RegistrarConclusaoProcessamento()
    {
        GarantirCorrelacao();
        Registrar(NivelEventoPoc.Info, "Whisper", "transcricao.concluida", "Transcrição local concluída.", 2_184);
        Registrar(NivelEventoPoc.Info, "Ata", "ata.gerada", "Ata estruturada validada e renderizada.", 826);
        Registrar(NivelEventoPoc.Info, "Arquivo", "reuniao.arquivada", "Artefatos movidos para o arquivo local.", 22);
        _jobsNaFila = 0;
        Registrar(NivelEventoPoc.Info, "Worker", "job.concluido", "Processamento concluído com sucesso.", 3_081);
    }

    public void RegistrarFalhaLocal(
        string componente,
        string evento,
        string mensagem)
    {
        _correlacaoAtual = "desktop-local";
        Registrar(
            NivelEventoPoc.Erro,
            componente,
            evento,
            mensagem);
    }

    private void CriarHistoricoSeguro()
    {
        var agora = _agora();
        _eventos.Add(new EventoObservabilidadePoc(
            agora.AddSeconds(-42),
            NivelEventoPoc.Info,
            "Tray",
            "diagnostico.concluido",
            "Pré-requisitos locais verificados.",
            "sistema-01",
            48));
        _eventos.Add(new EventoObservabilidadePoc(
            agora.AddSeconds(-49),
            NivelEventoPoc.Aviso,
            "OBS",
            "audio.nivel_baixo",
            "Nível do microfone ficou baixo por três segundos.",
            "captura-08"));
        _eventos.Add(new EventoObservabilidadePoc(
            agora.AddMinutes(-4),
            NivelEventoPoc.Info,
            "Worker",
            "job.concluido",
            "Processamento local anterior foi concluído.",
            "reuniao-07",
            8_420));
        _eventos.Add(new EventoObservabilidadePoc(
            agora.AddMinutes(-4).AddSeconds(-7),
            NivelEventoPoc.Info,
            "Whisper",
            "transcricao.concluida",
            "Transcrição local anterior foi concluída.",
            "reuniao-07",
            6_380));
        _eventos.Add(new EventoObservabilidadePoc(
            agora.AddMinutes(-6),
            NivelEventoPoc.Info,
            "Retencao",
            "retencao.avaliada",
            "Política avaliada sem remoção pendente.",
            "retencao-03",
            12));
    }

    private void Registrar(
        NivelEventoPoc nivel,
        string componente,
        string evento,
        string mensagem,
        double? duracaoMs = null) =>
        _eventos.Insert(0, new EventoObservabilidadePoc(
            _agora(),
            nivel,
            componente,
            evento,
            Sanitizar(mensagem),
            _correlacaoAtual!,
            duracaoMs));

    private void GarantirCorrelacao()
    {
        if (_correlacaoAtual is not null)
        {
            return;
        }

        _sequenciaCorrelacao++;
        _correlacaoAtual = $"sim-reuniao-{_sequenciaCorrelacao:00}";
    }

    private static bool Contem(EventoObservabilidadePoc evento, string filtro) =>
        evento.Componente.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
        evento.Evento.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
        evento.Mensagem.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
        evento.CorrelacaoId.Contains(filtro, StringComparison.OrdinalIgnoreCase);

    private static string Sanitizar(string mensagem) =>
        mensagem.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static string FormatarDuracao(TimeSpan duracao) =>
        $"{(int)duracao.TotalMinutes:00}:{duracao.Seconds:00}";
}
