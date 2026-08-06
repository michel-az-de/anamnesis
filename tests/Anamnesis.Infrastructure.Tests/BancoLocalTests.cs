using System.Diagnostics;
using Anamnesis.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class BancoLocalTests : IAsyncLifetime
{
    private static readonly TimeSpan LimiteTeste = TimeSpan.FromSeconds(2);
    private readonly string _diretorio = Path.Combine(
        Path.GetTempPath(),
        $"anamnesis-banco-local-{Guid.NewGuid():N}");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_diretorio);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_diretorio))
        {
            Directory.Delete(_diretorio, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task DeveSerializarPreparacaoDoMesmoBancoEntreInstancias()
    {
        var caminho = Path.Combine(_diretorio, "journal.db");
        var primeiraEntrou = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var liberar = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var segundaDetectouContencao = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var quantidadePreparacoes = 0;
        var esquemaPronto = 0;

        async Task AplicarEsquemaAsync(
            SqliteConnection conexao,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref quantidadePreparacoes);
            primeiraEntrou.TrySetResult();
            await liberar.Task.WaitAsync(cancellationToken);
            Volatile.Write(ref esquemaPronto, 1);
        }

        Task<bool> EsquemaPreparadoAsync(
            SqliteConnection conexao,
            CancellationToken cancellationToken) =>
            Task.FromResult(Volatile.Read(ref esquemaPronto) == 1);

        var primeira = CriarBanco(caminho, AplicarEsquemaAsync, EsquemaPreparadoAsync);
        var segunda = CriarBanco(
            caminho,
            AplicarEsquemaAsync,
            EsquemaPreparadoAsync,
            () => segundaDetectouContencao.TrySetResult());
        var abrirPrimeira = primeira.AbrirAsync(CancellationToken.None);
        await primeiraEntrou.Task.WaitAsync(LimiteTeste);

        var abrirSegunda = segunda.AbrirAsync(CancellationToken.None);
        await segundaDetectouContencao.Task.WaitAsync(LimiteTeste);

        Assert.False(abrirSegunda.IsCompleted);
        Assert.Equal(1, Volatile.Read(ref quantidadePreparacoes));
        liberar.TrySetResult();
        await using var conexaoPrimeira = await abrirPrimeira.WaitAsync(LimiteTeste);
        await using var conexaoSegunda = await abrirSegunda.WaitAsync(LimiteTeste);
        Assert.Equal(1, Volatile.Read(ref quantidadePreparacoes));
        Assert.Equal(1, primeira.Preparacoes);
        Assert.Equal(0, segunda.Preparacoes);
    }

    [Fact]
    public async Task DevePermitirPreparacaoParalelaDeBancosDiferentes()
    {
        var ambasEntraram = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var liberar = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var quantidade = 0;

        async Task AplicarEsquemaAsync(
            SqliteConnection conexao,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref quantidade) == 2)
            {
                ambasEntraram.TrySetResult();
            }

            await liberar.Task.WaitAsync(cancellationToken);
        }

        var primeira = CriarBanco(
            Path.Combine(_diretorio, "primeiro.db"),
            AplicarEsquemaAsync);
        var segunda = CriarBanco(
            Path.Combine(_diretorio, "segundo.db"),
            AplicarEsquemaAsync);
        var abrirPrimeira = primeira.AbrirAsync(CancellationToken.None);
        var abrirSegunda = segunda.AbrirAsync(CancellationToken.None);

        await ambasEntraram.Task.WaitAsync(LimiteTeste);
        liberar.TrySetResult();
        await using var conexaoPrimeira = await abrirPrimeira.WaitAsync(LimiteTeste);
        await using var conexaoSegunda = await abrirSegunda.WaitAsync(LimiteTeste);
    }

    [Fact]
    public async Task DeveLimitarEsperaPorTravaMantidaPorOutroHandle()
    {
        var caminho = Path.Combine(_diretorio, "bloqueado.db");
        await using var bloqueador = new FileStream(
            caminho + ".init.lock",
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 1,
            FileOptions.None);
        var banco = new BancoLocal(
            caminho,
            static (conexao, cancellationToken) => Task.CompletedTask,
            limiteExclusividadePreparacao: TimeSpan.FromMilliseconds(150));
        var cronometro = Stopwatch.StartNew();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            banco.AbrirAsync(CancellationToken.None));

        cronometro.Stop();
        Assert.InRange(
            cronometro.Elapsed,
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task DeveCancelarContencaoSemImpedirNovaAquisicao()
    {
        var caminho = Path.Combine(_diretorio, "cancelado.db");
        var primeiraEntrou = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var liberarPrimeira = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var segundaDetectouContencao = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task SegurarPreparacaoAsync(
            SqliteConnection conexao,
            CancellationToken cancellationToken)
        {
            primeiraEntrou.TrySetResult();
            await liberarPrimeira.Task.WaitAsync(cancellationToken);
        }

        var primeira = CriarBanco(caminho, SegurarPreparacaoAsync);
        var abrirPrimeira = primeira.AbrirAsync(CancellationToken.None);
        await primeiraEntrou.Task.WaitAsync(LimiteTeste);
        using var cancelamento = new CancellationTokenSource();
        var segunda = CriarBanco(
            caminho,
            static (conexao, cancellationToken) => Task.CompletedTask,
            aoDetectarContencao: () => segundaDetectouContencao.TrySetResult());
        var abrirSegunda = segunda.AbrirAsync(cancelamento.Token);

        try
        {
            await segundaDetectouContencao.Task.WaitAsync(LimiteTeste);
            cancelamento.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => abrirSegunda);
        }
        finally
        {
            liberarPrimeira.TrySetResult();
        }

        await using var conexaoPrimeira = await abrirPrimeira.WaitAsync(LimiteTeste);
        var terceiraPreparou = 0;
        var terceira = CriarBanco(
            caminho,
            (conexao, cancellationToken) =>
            {
                Interlocked.Increment(ref terceiraPreparou);
                return Task.CompletedTask;
            });
        await using var conexaoTerceira = await terceira.AbrirAsync(CancellationToken.None)
            .WaitAsync(LimiteTeste);
        Assert.Equal(1, Volatile.Read(ref terceiraPreparou));
    }

    private static BancoLocal CriarBanco(
        string caminho,
        Func<SqliteConnection, CancellationToken, Task> aplicarEsquema,
        Func<SqliteConnection, CancellationToken, Task<bool>>? esquemaPreparado = null,
        Action? aoDetectarContencao = null) =>
        new(
            caminho,
            aplicarEsquema,
            limiteExclusividadePreparacao: TimeSpan.FromSeconds(5),
            esquemaPreparado: esquemaPreparado,
            aoDetectarContencaoPreparacao: aoDetectarContencao);
}
