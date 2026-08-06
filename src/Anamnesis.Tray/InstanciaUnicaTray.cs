namespace Anamnesis.Tray;

internal sealed class InstanciaUnicaTray : IDisposable
{
    private const string Prefixo = @"Local\Anamnesis.Tray.";
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _ativacao;
    private readonly EventWaitHandle _encerramentoParaAtualizacao;
    private readonly ManualResetEvent _encerrarObservadores = new(initialState: false);
    private Thread? _observador;
    private Thread? _observadorEncerramento;
    private bool _liberado;
    private bool _descartado;

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

        _observador ??= CriarObservador(
            _ativacao,
            acao,
            "Anamnesis.Tray.Ativacao");
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

        _observadorEncerramento ??= CriarObservador(
            _encerramentoParaAtualizacao,
            acao,
            "Anamnesis.Tray.Encerramento");
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
        if (_descartado)
        {
            return;
        }

        _descartado = true;
        _encerrarObservadores.Set();
        AguardarObservador(_observador);
        _observador = null;
        AguardarObservador(_observadorEncerramento);
        _observadorEncerramento = null;
        _ativacao.Dispose();
        _encerramentoParaAtualizacao.Dispose();
        _encerrarObservadores.Dispose();
        if (EhPrimaria && !_liberado)
        {
            _liberado = true;
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }

    private Thread CriarObservador(EventWaitHandle evento, Action acao, string nome)
    {
        var observador = new Thread(() => Observar(evento, acao))
        {
            IsBackground = true,
            Name = nome
        };
        observador.Start();
        return observador;
    }

    private void Observar(EventWaitHandle evento, Action acao)
    {
        var sinais = new WaitHandle[] { evento, _encerrarObservadores };
        while (WaitHandle.WaitAny(sinais) == 0)
        {
            if (_encerrarObservadores.WaitOne(TimeSpan.Zero))
            {
                return;
            }

            acao();
        }
    }

    private static void AguardarObservador(Thread? observador)
    {
        if (observador is not null && observador != Thread.CurrentThread)
        {
            observador.Join();
        }
    }
}
