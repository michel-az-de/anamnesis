using Anamnesis.Application.Modelos;

namespace Anamnesis.Tray;

internal sealed record DesktopRuntimeInfo(
    string CaminhoConfiguracao,
    string CaminhoBanco,
    string DiretorioArquivo,
    string NomeCli,
    IReadOnlyList<ProntidaoDesktopItem>? Prontidao = null,
    string ModoDeteccao = "Assistido");

internal sealed record ProntidaoDesktopItem(
    string Nome,
    bool Disponivel,
    string Mensagem);

internal interface IDesktopSession
{
    bool ModoDemonstracao { get; }

    EtapaDesktopPoc Etapa { get; }

    TimeSpan DuracaoGravacao { get; }

    IReadOnlyList<ReuniaoDesktopPoc> Reunioes { get; }

    IReadOnlyList<EventoObservabilidadePoc> EventosOperacionais => [];

    int JobsNaFila => 0;

    bool RecuperacaoPendente => false;

    bool Inicializada => true;

    Guid? ReuniaoAtivaId => null;

    Guid? ReuniaoAcompanhadaId => ReuniaoAtivaId;

    DesktopRuntimeInfo? Ambiente => null;

    NivelAudioLeitura NivelAudio => NivelAudioLeitura.SemLeitura();

    Task AtualizarAsync(CancellationToken cancellationToken);

    Task AtualizarNivelAudioAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    Task IniciarGravacaoAsync(string titulo, CancellationToken cancellationToken);

    void AvancarGravacao();

    Task EncerrarGravacaoAsync(CancellationToken cancellationToken);

    void ConcluirProcessamentoSimulado();

    Task<ReuniaoDesktopPoc?> ObterDetalheAsync(
        Guid reuniaoId,
        CancellationToken cancellationToken);

    Task SalvarEdicaoAsync(
        Guid reuniaoId,
        string titulo,
        string transcricao,
        CancellationToken cancellationToken) =>
        Task.FromException(new NotSupportedException("A sessão não permite editar reuniões."));

    Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken);

    Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken);

    Task RegistrarFalhaOperacionalAsync(
        string operacao,
        Exception exception,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
