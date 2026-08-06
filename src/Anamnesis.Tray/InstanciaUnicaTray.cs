namespace Anamnesis.Tray;

internal sealed class InstanciaUnicaTray : IDisposable
{
    private const string Prefixo = @"Local\Anamnesis.Tray.";
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _ativacao;
    private readonly EventWaitHandle _encerramentoParaAtualizacao;
    private RegisteredWaitHandle? _observador;
    private RegisteredWaitHandle? _observadorEncerramento;
    private bool _liberado;

    private InstanciaUnicaTray(
        Mutex mutex,
        EventWaitHandle ativacao,
        EventWaitHandle encerramentoParaAtualizacao,
        bool ehPrimaria)
    {
        _mutex = mutex;
        _ativacao = ativacao;
        _encerramentoParaAtualizacao = encerramentoParaAtualizacao;
        EhPrimaria = ehPrimaria;
    }

    public bool EhPrimaria { get; }

    public static InstanciaUnicaTray Criar(string? chave = null)
    {
        var sufixo = string.IsNullOrWhiteSpace(chave)
            ? Environment.UserName
            : chave;
        var nome = Prefixo + sufixo;
        var mutex = new Mutex(initiallyOwned: true, nome, out var criado);
        var ativacao = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            nome + ".Ativar");
        var encerramentoParaAtualizacao = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            nome + ".EncerrarParaAtualizacao");
        return new InstanciaUnicaTray(mutex, ativacao, encerramentoParaAtualizacao, criado);
    }

    public void ObservarAtivacao(Action acao)
    {
        ArgumentNullException.ThrowIfNull(acao);
        if (!EhPrimaria)
        {
            throw new InvalidOperationException("Somente a instancia primaria observa ativacao.");
        }

        _observador ??= ThreadPool.RegisterWaitForSingleObject(
            _ativacao,
            (_, _) => acao(),
            state: null,
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: false);
    }

    public void SinalizarPrimeiraInstancia()
    {
        if (EhPrimaria)
        {
            return;
        }

        _ativacao.Set();
    }

    public void ObservarEncerramentoParaAtualizacao(Action acao)
    {
        ArgumentNullException.ThrowIfNull(acao);
        if (!EhPrimaria)
        {
            throw new InvalidOperationException("Somente a instancia primaria observa o encerramento para atualizacao.");
        }

        _observadorEncerramento ??= ThreadPool.RegisterWaitForSingleObject(
            _encerramentoParaAtualizacao,
            (_, _) => acao(),
            state: null,
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: false);
    }

    public void SinalizarEncerramentoParaAtualizacao()
    {
        if (EhPrimaria)
        {
            return;
        }

        _encerramentoParaAtualizacao.Set();
    }

    public void Dispose()
    {
        _observador?.Unregister(null);
        _observador = null;
        _observadorEncerramento?.Unregister(null);
        _observadorEncerramento = null;
        _ativacao.Dispose();
        _encerramentoParaAtualizacao.Dispose();
        if (EhPrimaria && !_liberado)
        {
            _liberado = true;
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
