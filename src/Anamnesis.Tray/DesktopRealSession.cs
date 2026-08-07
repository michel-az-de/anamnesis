using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Anamnesis.Application.UseCases;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;

namespace Anamnesis.Tray;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "A sessão e seu semáforo vivem durante todo o processo do Tray.")]
internal sealed class DesktopRealSession(
    IReuniaoQuery reuniaoQuery,
    IJobQuery jobQuery,
    ControlarGravacaoHandler controlarGravacao,
    IArtefatoLauncher artefatoLauncher,
    TimeProvider relogio,
    DesktopRuntimeInfo? ambiente = null,
    IEventoOperacionalQuery? eventoQuery = null,
    IJobMetricasQuery? jobMetricasQuery = null,
    JornalOperacional? journal = null,
    INivelAudioSource? nivelAudioSource = null,
    EditarReuniaoHandler? editarReuniao = null) : IDesktopSession
{
    private IReadOnlyList<ReuniaoDesktopPoc> _reunioes = [];
    private IReadOnlyList<EventoObservabilidadePoc> _eventosOperacionais = [];
    private Guid? _reuniaoAcompanhadaId;
    private Guid? _gravacaoIniciadaNestaSessaoId;
    private DateTimeOffset? _gravacaoIniciadaEm;
    private readonly SemaphoreSlim _comandos = new(1, 1);
    private readonly JornalOperacional _journal = journal ?? JornalOperacional.Nulo;
    private int _operacoesGravacaoEmAndamento;

    public bool ModoDemonstracao => false;

    public EtapaDesktopPoc Etapa { get; private set; } = EtapaDesktopPoc.Pronto;

    public TimeSpan DuracaoGravacao
    {
        get
        {
            if (Etapa != EtapaDesktopPoc.Gravando || _gravacaoIniciadaEm is null)
            {
                return TimeSpan.Zero;
            }

            var duracao = relogio.GetUtcNow() - _gravacaoIniciadaEm.Value;
            return duracao < TimeSpan.Zero ? TimeSpan.Zero : duracao;
        }
    }

    public IReadOnlyList<ReuniaoDesktopPoc> Reunioes => _reunioes;

    public IReadOnlyList<EventoObservabilidadePoc> EventosOperacionais => _eventosOperacionais;

    public int JobsNaFila { get; private set; }

    public bool RecuperacaoPendente { get; private set; }

    public bool Inicializada { get; private set; }

    public bool PodeEncerrarParaAtualizacao =>
        Volatile.Read(ref _operacoesGravacaoEmAndamento) == 0 &&
        Etapa != EtapaDesktopPoc.Gravando;

    public Guid? ReuniaoAtivaId =>
        Etapa == EtapaDesktopPoc.Gravando ? _reuniaoAcompanhadaId : null;

    public Guid? ReuniaoAcompanhadaId => _reuniaoAcompanhadaId;

    public DesktopRuntimeInfo? Ambiente => ambiente;

    public NivelAudioLeitura NivelAudio { get; private set; } = NivelAudioLeitura.SemLeitura();

    public async Task AtualizarAsync(CancellationToken cancellationToken)
    {
        await _comandos.WaitAsync(cancellationToken);
        try
        {
            await AtualizarSemTravaAsync(cancellationToken);
        }
        finally
        {
            _comandos.Release();
        }
    }

    public async Task AtualizarNivelAudioAsync(CancellationToken cancellationToken)
    {
        if (nivelAudioSource is null)
        {
            NivelAudio = NivelAudioLeitura.SemLeitura();
            return;
        }

        try
        {
            NivelAudio = await nivelAudioSource.LerAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            NivelAudio = NivelAudioLeitura.SemLeitura("Core Audio não respondeu à leitura local.");
        }
    }

    /// <summary>
    /// Só pode ser chamado por quem já detém <see cref="_comandos"/>. O estado da sessão é lido
    /// pelo timer de polling e escrito pelos comandos: sem a trava, os dois se intercalam nos
    /// pontos de await.
    /// </summary>
    private async Task AtualizarSemTravaAsync(CancellationToken cancellationToken)
    {
        var resumos = await reuniaoQuery.ListarAsync(
            new ReuniaoQueryFiltro(null, null, 100),
            cancellationToken);
        var gravacaoPersistida = resumos.FirstOrDefault(
            reuniao => reuniao.Status == StatusReuniao.Gravando);

        // Uma gravacao criada por outro processo ou antes do reinicio exige decisao humana.
        // Nao consultar o preflight aqui e intencional: ele poderia iniciar o OBS sem consentimento.
        RecuperacaoPendente = gravacaoPersistida is not null &&
            gravacaoPersistida.Id != _gravacaoIniciadaNestaSessaoId;

        _reunioes = resumos.Select(MapearResumo).ToArray();
        await AtualizarObservabilidadeAsync(cancellationToken);
        AtualizarEtapa(resumos);
        Inicializada = true;
    }

    private async Task AtualizarObservabilidadeAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (eventoQuery is not null)
            {
                var eventos = await eventoQuery.ListarAsync(
                    new EventoOperacionalFiltro(Limite: 500),
                    cancellationToken);
                _eventosOperacionais = eventos.Select(MapearEventoOperacional).ToArray();
            }
        }
        catch (Exception)
        {
            // O journal e best-effort: corrupcao ou contencao nao muda o resultado do comando.
            _eventosOperacionais = [];
        }

        try
        {
            JobsNaFila = jobMetricasQuery is null
                ? 0
                : await jobMetricasQuery.ContarPendentesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // A metrica e diagnostica e nao participa do estado da reuniao.
            JobsNaFila = 0;
        }
    }

    public async Task IniciarGravacaoAsync(string titulo, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _operacoesGravacaoEmAndamento);
        try
        {
            await _comandos.WaitAsync(cancellationToken);
            try
            {
                if (Etapa == EtapaDesktopPoc.Gravando)
                {
                    throw new InvalidOperationException("Já existe uma gravação em andamento.");
                }

                var tituloNormalizado = string.IsNullOrWhiteSpace(titulo)
                    ? "Reunião sem título"
                    : titulo.Trim();
                _reuniaoAcompanhadaId = await controlarGravacao.IniciarAsync(
                    tituloNormalizado,
                    cancellationToken);
                _gravacaoIniciadaNestaSessaoId = _reuniaoAcompanhadaId;
                await AtualizarSemTravaAsync(cancellationToken);
            }
            finally
            {
                _comandos.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _operacoesGravacaoEmAndamento);
        }
    }

    public void AvancarGravacao()
    {
    }

    public async Task EncerrarGravacaoAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _operacoesGravacaoEmAndamento);
        try
        {
            await _comandos.WaitAsync(cancellationToken);
            try
            {
                await AtualizarSemTravaAsync(cancellationToken);
                if (Etapa != EtapaDesktopPoc.Gravando)
                {
                    throw new InvalidOperationException("Não existe gravação em andamento.");
                }

                var reuniaoId = _reuniaoAcompanhadaId
                    ?? throw new InvalidOperationException("Não existe gravação em andamento.");
                try
                {
                    await controlarGravacao.FinalizarAsync(reuniaoId, cancellationToken);
                }
                finally
                {
                    _gravacaoIniciadaNestaSessaoId = null;
                    await AtualizarSemTravaAsync(CancellationToken.None);
                }
            }
            finally
            {
                _comandos.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _operacoesGravacaoEmAndamento);
        }
    }

    public void ConcluirProcessamentoSimulado()
    {
    }

    public async Task<ReuniaoDesktopPoc?> ObterDetalheAsync(
        Guid reuniaoId,
        CancellationToken cancellationToken)
    {
        var detalhe = await reuniaoQuery.ObterDetalheAsync(reuniaoId, cancellationToken);
        if (detalhe is null)
        {
            return null;
        }

        var job = await jobQuery.ObterMaisRecenteAsync(reuniaoId, cancellationToken);
        return MapearDetalhe(detalhe, job);
    }

    public async Task SalvarEdicaoAsync(
        Guid reuniaoId,
        string titulo,
        string transcricao,
        CancellationToken cancellationToken)
    {
        if (editarReuniao is null)
        {
            throw new NotSupportedException("A edição de reuniões não está configurada.");
        }

        await _comandos.WaitAsync(cancellationToken);
        try
        {
            await editarReuniao.ExecutarAsync(
                reuniaoId,
                titulo,
                transcricao,
                cancellationToken);
            await AtualizarSemTravaAsync(cancellationToken);
        }
        finally
        {
            _comandos.Release();
        }
    }

    public Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken) =>
        artefatoLauncher.AbrirArquivoAsync(caminho, cancellationToken);

    public Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken) =>
        artefatoLauncher.MostrarNaPastaAsync(caminho, cancellationToken);

    public Task RegistrarFalhaOperacionalAsync(
        string operacao,
        Exception exception,
        CancellationToken cancellationToken) =>
        _journal.RegistrarFalhaAsync(
            "Desktop",
            operacao,
            exception,
            null,
            null,
            cancellationToken);

    private void AtualizarEtapa(IReadOnlyList<ReuniaoResumo> resumos)
    {
        var gravando = resumos.FirstOrDefault(reuniao => reuniao.Status == StatusReuniao.Gravando);
        if (gravando is not null)
        {
            _reuniaoAcompanhadaId = gravando.Id;
            _gravacaoIniciadaEm = gravando.GravacaoIniciadaEm ?? gravando.CriadaEm;
            Etapa = EtapaDesktopPoc.Gravando;
            return;
        }

        _gravacaoIniciadaEm = null;
        var acompanhada = _reuniaoAcompanhadaId is null
            ? null
            : resumos.FirstOrDefault(reuniao => reuniao.Id == _reuniaoAcompanhadaId);
        if (acompanhada is not null)
        {
            Etapa = acompanhada.Status switch
            {
                StatusReuniao.AguardandoProcessamento or
                StatusReuniao.EmTranscricao or
                StatusReuniao.EmAnalise or
                StatusReuniao.AguardandoArquivamento => EtapaDesktopPoc.Processando,
                StatusReuniao.Arquivada or
                StatusReuniao.PendenteExclusao or
                StatusReuniao.Excluida => EtapaDesktopPoc.Concluido,
                _ => EtapaDesktopPoc.Pronto
            };
            return;
        }

        var emProcessamento = resumos.FirstOrDefault(reuniao => reuniao.Status is
            StatusReuniao.AguardandoProcessamento or
            StatusReuniao.EmTranscricao or
            StatusReuniao.EmAnalise or
            StatusReuniao.AguardandoArquivamento);
        if (emProcessamento is not null)
        {
            _reuniaoAcompanhadaId = emProcessamento.Id;
            Etapa = EtapaDesktopPoc.Processando;
            return;
        }

        Etapa = EtapaDesktopPoc.Pronto;
    }

    private ReuniaoDesktopPoc MapearResumo(ReuniaoResumo resumo) => new()
    {
        Id = resumo.Id,
        Titulo = resumo.Titulo,
        Data = FormatarData(resumo.CriadaEm),
        Plataforma = "Captura OBS",
        Duracao = FormatarDuracao(
            resumo.GravacaoIniciadaEm,
            resumo.GravacaoFinalizadaEm,
            resumo.Status == StatusReuniao.Gravando ? relogio.GetUtcNow() : null),
        Status = FormatarStatus(resumo.Status),
        Resumo = resumo.MotivoFalha ?? DescreverStatus(resumo.Status),
        PontosPrincipais = CriarMetadadosResumo(resumo),
        Transcricao = [],
        Decisoes = [],
        Tarefas = [],
        MotivoFalha = resumo.MotivoFalha
    };

    private static ReuniaoDesktopPoc MapearDetalhe(ReuniaoDetalhe detalhe, JobResumo? job) => new()
    {
        Id = detalhe.Id,
        Titulo = detalhe.Titulo,
        Data = FormatarData(detalhe.CriadaEm),
        Plataforma = "Captura OBS",
        Duracao = FormatarDuracao(
            detalhe.GravacaoIniciadaEm,
            detalhe.GravacaoFinalizadaEm,
            null),
        Status = FormatarStatus(detalhe.Status),
        Resumo = detalhe.ResumoExecutivo ?? detalhe.MotivoFalha ?? DescreverStatus(detalhe.Status),
        PontosPrincipais = CriarMetadadosDetalhe(detalhe, job),
        Transcricao = SepararLinhas(detalhe.TextoTranscricao),
        Decisoes = detalhe.Decisoes,
        Tarefas = detalhe.Tarefas.Select(FormatarTarefa).ToArray(),
        MotivoFalha = detalhe.MotivoFalha,
        CaminhoAta = detalhe.Artefatos?.CaminhoAta,
        CaminhoTranscricao = detalhe.Artefatos?.CaminhoTranscricao,
        CaminhoGravacao = detalhe.CaminhoGravacao,
        DiretorioArtefatos = detalhe.Artefatos?.Diretorio
    };

    private static List<string> CriarMetadadosResumo(ReuniaoResumo resumo)
    {
        var itens = new List<string>
        {
            $"Identificador: {resumo.Id:N}",
            $"Estado: {FormatarStatus(resumo.Status)}",
            $"Criada em: {FormatarData(resumo.CriadaEm)}"
        };
        if (!string.IsNullOrWhiteSpace(resumo.MotivoFalha))
        {
            itens.Add($"Falha: {resumo.MotivoFalha}");
        }

        return itens;
    }

    private static List<string> CriarMetadadosDetalhe(
        ReuniaoDetalhe detalhe,
        JobResumo? job)
    {
        var itens = new List<string>
        {
            $"Identificador: {detalhe.Id:N}",
            $"Estado: {FormatarStatus(detalhe.Status)}",
            $"Criada em: {FormatarData(detalhe.CriadaEm)}"
        };
        AdicionarData(itens, "Gravação iniciada", detalhe.GravacaoIniciadaEm);
        AdicionarData(itens, "Gravação finalizada", detalhe.GravacaoFinalizadaEm);
        AdicionarData(itens, "Transcrição gerada", detalhe.TranscricaoGeradaEm);
        AdicionarData(itens, "Ata gerada", detalhe.AtaGeradaEm);
        AdicionarData(itens, "Arquivada", detalhe.ArquivadaEm);
        if (!string.IsNullOrWhiteSpace(detalhe.MotivoFalha))
        {
            itens.Add($"Falha: {detalhe.MotivoFalha}");
        }

        if (job is not null)
        {
            itens.Add($"Job: {job.Id:N} | {job.Estado} | tentativas: {job.Tentativas}");
            AdicionarData(itens, "Job reservado", job.ReservadoEm);
            AdicionarData(itens, "Job concluído", job.ConcluidoEm);
        }

        return itens;
    }

    private static void AdicionarData(List<string> itens, string rotulo, DateTimeOffset? valor)
    {
        if (valor is not null)
        {
            itens.Add($"{rotulo}: {FormatarData(valor.Value)}");
        }
    }

    private static string[] SepararLinhas(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? []
            : texto.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string FormatarTarefa(Tarefa tarefa)
    {
        var responsavel = string.IsNullOrWhiteSpace(tarefa.Responsavel)
            ? "Responsável não informado"
            : tarefa.Responsavel;
        var prazo = tarefa.Prazo?.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("pt-BR"))
            ?? "sem prazo";
        return $"{tarefa.Descricao} | {responsavel} | {prazo}";
    }

    private static string FormatarData(DateTimeOffset valor) =>
        valor.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"));

    private static string FormatarDuracao(
        DateTimeOffset? inicio,
        DateTimeOffset? fim,
        DateTimeOffset? agora)
    {
        if (inicio is null)
        {
            return "Não iniciada";
        }

        var limite = fim ?? agora;
        if (limite is null)
        {
            return "Em andamento";
        }

        var duracao = limite.Value - inicio.Value;
        if (duracao < TimeSpan.Zero)
        {
            duracao = TimeSpan.Zero;
        }

        return $"{(int)duracao.TotalHours:00}:{duracao.Minutes:00}:{duracao.Seconds:00}";
    }

    private static string FormatarStatus(StatusReuniao status) => status switch
    {
        StatusReuniao.AguardandoProcessamento => "Aguardando processamento",
        StatusReuniao.EmTranscricao => "Transcrevendo",
        StatusReuniao.EmAnalise => "Gerando ata",
        StatusReuniao.AguardandoArquivamento => "Arquivando",
        StatusReuniao.Arquivada => "Ata pronta",
        StatusReuniao.PendenteExclusao => "Retenção pendente",
        StatusReuniao.Excluida => "Gravação removida",
        StatusReuniao.Falha => "Falha",
        _ => status.ToString()
    };

    private static string DescreverStatus(StatusReuniao status) => status switch
    {
        StatusReuniao.Gravando => "Gravação local em andamento pelo OBS.",
        StatusReuniao.AguardandoProcessamento => "Gravação salva e aguardando o Worker local.",
        StatusReuniao.EmTranscricao => "Whisper está transcrevendo a gravação localmente.",
        StatusReuniao.EmAnalise => "A ata estruturada está sendo gerada.",
        StatusReuniao.AguardandoArquivamento => "Os artefatos estão sendo arquivados.",
        StatusReuniao.Arquivada => "Ata e transcrição disponíveis no arquivo local.",
        StatusReuniao.PendenteExclusao => "A gravação aguarda aplicação da retenção.",
        StatusReuniao.Excluida => "A retenção foi aplicada à gravação.",
        StatusReuniao.Falha => "O processamento falhou. Consulte o motivo e tente processar as pendências.",
        _ => "Reunião registrada localmente."
    };

    private static EventoObservabilidadePoc MapearEventoOperacional(EventoOperacional evento) =>
        new(
            evento.CriadoEm,
            evento.Nivel switch
            {
                NivelEventoOperacional.Aviso => NivelEventoPoc.Aviso,
                NivelEventoOperacional.Erro => NivelEventoPoc.Erro,
                _ => NivelEventoPoc.Info
            },
            evento.Componente,
            evento.Codigo,
            evento.Mensagem,
            FormatarCorrelacao(evento),
            evento.Metadados.DuracaoMs);

    private static string FormatarCorrelacao(EventoOperacional evento)
    {
        var partes = new List<string>(2);
        if (evento.ReuniaoId is not null)
        {
            partes.Add($"r:{evento.ReuniaoId:N}");
        }

        if (evento.JobId is not null)
        {
            partes.Add($"j:{evento.JobId:N}");
        }

        return partes.Count == 0 ? "sistema" : string.Join(' ', partes);
    }
}
