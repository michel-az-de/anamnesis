using Anamnesis.Application.Modelos;

namespace Anamnesis.Tray;

internal sealed record DesktopRuntimeInfo(
    string CaminhoConfiguracao,
    string CaminhoBanco,
    string DiretorioArquivo,
    string NomeCli,
    IReadOnlyList<ProntidaoDesktopItem>? Prontidao = null);

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

    Task AtualizarAsync(CancellationToken cancellationToken);

    Task IniciarGravacaoAsync(string titulo, CancellationToken cancellationToken);

    void AvancarGravacao();

    Task EncerrarGravacaoAsync(CancellationToken cancellationToken);

    void ConcluirProcessamentoSimulado();

    Task<ReuniaoDesktopPoc?> ObterDetalheAsync(
        Guid reuniaoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ReuniaoDesktopPoc>> BuscarReunioesAsync(
        string? texto,
        string? status,
        DateTimeOffset? criadaDesde,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ReuniaoDesktopPoc>>(
            Reunioes
                .Where(reuniao =>
                    (string.IsNullOrWhiteSpace(texto) ||
                     reuniao.Titulo.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                     reuniao.Plataforma.Contains(texto, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrWhiteSpace(status) ||
                     string.Equals(status, "Todos os estados", StringComparison.Ordinal) ||
                     string.Equals(reuniao.Status, status, StringComparison.Ordinal)))
                .ToArray());

    Task<string> ExportarAtaAsync(
        Guid reuniaoId,
        FormatoExportacaoAta formato,
        string caminhoDestino,
        bool sobrescrever,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("A exportação de atas não está configurada.");

    Task<string> PublicarAtaObsidianAsync(
        Guid reuniaoId,
        string caminhoVault,
        string subpasta,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("A publicação no Obsidian não está configurada.");

    Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken);

    Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken);

    Task RegistrarFalhaOperacionalAsync(
        string operacao,
        Exception exception,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
