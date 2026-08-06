namespace Anamnesis.Tray;

internal sealed class DesktopDemoSession : IDesktopSession
{
    private readonly DesktopPocState _state = new();

    public bool ModoDemonstracao => true;

    public EtapaDesktopPoc Etapa => _state.Etapa;

    public TimeSpan DuracaoGravacao => _state.DuracaoGravacao;

    public IReadOnlyList<ReuniaoDesktopPoc> Reunioes => _state.Reunioes;

    public Guid? ReuniaoAcompanhadaId => _state.ReuniaoAcompanhadaId;

    public Task AtualizarAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task IniciarGravacaoAsync(string titulo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _state.IniciarGravacao(titulo);
        return Task.CompletedTask;
    }

    public void AvancarGravacao() => _state.AvancarGravacao();

    public Task EncerrarGravacaoAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _state.EncerrarGravacao();
        return Task.CompletedTask;
    }

    public void ConcluirProcessamentoSimulado() => _state.ConcluirProcessamento();

    public Task<ReuniaoDesktopPoc?> ObterDetalheAsync(
        Guid reuniaoId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<ReuniaoDesktopPoc?>(
            _state.Reunioes.SingleOrDefault(reuniao => reuniao.Id == reuniaoId));
    }

    public Task AbrirArquivoAsync(string caminho, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task MostrarNaPastaAsync(string caminho, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
