namespace Anamnesis.Tray;

internal sealed record DesktopRuntimeInfo(
    string CaminhoConfiguracao,
    string CaminhoBanco,
    string DiretorioArquivo,
    string NomeCli);

internal interface IDesktopSession
{
    bool ModoDemonstracao { get; }

    EtapaDesktopPoc Etapa { get; }

    TimeSpan DuracaoGravacao { get; }

    IReadOnlyList<ReuniaoDesktopPoc> Reunioes { get; }

    IReadOnlyList<EventoObservabilidadePoc> EventosOperacionais => [];

    int JobsNaFila => 0;

    DesktopRuntimeInfo? Ambiente => null;

    Task AtualizarAsync(CancellationToken cancellationToken);

    Task IniciarGravacaoAsync(string titulo, CancellationToken cancellationToken);

    void AvancarGravacao();

    Task EncerrarGravacaoAsync(CancellationToken cancellationToken);

    void ConcluirProcessamentoSimulado();

    Task<ReuniaoDesktopPoc?> ObterDetalheAsync(
        Guid reuniaoId,
        CancellationToken cancellationToken);

    Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken);

    Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken);

    Task RegistrarFalhaOperacionalAsync(
        string operacao,
        Exception exception,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
