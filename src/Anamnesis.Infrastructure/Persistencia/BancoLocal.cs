using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;

namespace Anamnesis.Infrastructure.Persistencia;

/// <summary>
/// Abre conexões para o banco local aplicando o esquema uma única vez por instância. Antes disso,
/// cada leitura pagava um `CREATE TABLE`, uma consulta a `pragma_table_info`, um `UPDATE` sobre a
/// tabela inteira e um `CREATE INDEX` — além de uma segunda conexão só para isso.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Os adaptadores SQLite e seu semáforo vivem durante todo o processo.")]
internal sealed class BancoLocal(
    string caminhoBanco,
    Func<SqliteConnection, CancellationToken, Task> aplicarEsquema,
    int? timeoutComandoSegundos = null,
    TimeSpan? limiteExclusividadePreparacao = null,
    Func<SqliteConnection, CancellationToken, Task<bool>>? esquemaPreparado = null,
    Action? aoDetectarContencaoPreparacao = null)
{
    private static readonly TimeSpan IntervaloTentativaTrava = TimeSpan.FromMilliseconds(25);
    private readonly string _connectionString = CriarConnectionString(
        caminhoBanco,
        timeoutComandoSegundos);
    private readonly TimeSpan? _limiteExclusividadePreparacao =
        ValidarLimiteExclusividade(limiteExclusividadePreparacao);
    private readonly string? _caminhoTravaPreparacao = limiteExclusividadePreparacao is null
        ? null
        : Path.GetFullPath(caminhoBanco) + ".init.lock";

    private readonly SemaphoreSlim _preparacao = new(1, 1);
    private int _preparacoes;
    private bool _preparado;

    /// <summary>
    /// Quantas vezes o esquema foi realmente aplicado. Existe para os testes provarem que a
    /// preparação não se repete a cada operação.
    /// </summary>
    public int Preparacoes => _preparacoes;

    public async Task<SqliteConnection> AbrirAsync(CancellationToken cancellationToken)
    {
        var conexao = new SqliteConnection(_connectionString);
        await conexao.OpenAsync(cancellationToken);
        try
        {
            await PrepararAsync(conexao, cancellationToken);
            return conexao;
        }
        catch
        {
            await conexao.DisposeAsync();
            throw;
        }
    }

    private static string CriarConnectionString(string caminhoBanco, int? timeoutComandoSegundos)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = caminhoBanco,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };
        if (timeoutComandoSegundos is not null)
        {
            builder.DefaultTimeout = timeoutComandoSegundos.Value;
        }

        return builder.ToString();
    }

    private async Task PrepararAsync(SqliteConnection conexao, CancellationToken cancellationToken)
    {
        if (_preparado)
        {
            return;
        }

        await _preparacao.WaitAsync(cancellationToken);
        try
        {
            if (_preparado)
            {
                return;
            }

            await using var trava = await AdquirirTravaPreparacaoAsync(cancellationToken);
            if (esquemaPreparado is not null &&
                await esquemaPreparado(conexao, cancellationToken))
            {
                _preparado = true;
                return;
            }

            // Tray e Worker compartilham o arquivo. Em WAL, leitor e escritor deixam de se
            // bloquear, o que importa porque o Desktop consulta o banco a cada dois segundos.
            await using (var wal = conexao.CreateCommand())
            {
                wal.CommandText = "PRAGMA journal_mode=WAL;";
                await wal.ExecuteNonQueryAsync(cancellationToken);
            }

            await aplicarEsquema(conexao, cancellationToken);
            _preparacoes++;
            _preparado = true;
        }
        finally
        {
            _preparacao.Release();
        }
    }

    private async Task<FileStream?> AdquirirTravaPreparacaoAsync(
        CancellationToken cancellationToken)
    {
        if (_caminhoTravaPreparacao is null || _limiteExclusividadePreparacao is null)
        {
            return null;
        }

        var cronometro = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    _caminhoTravaPreparacao,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.None);
            }
            catch (IOException exception) when (EhViolacaoDeCompartilhamento(exception))
            {
                aoDetectarContencaoPreparacao?.Invoke();
                var restante = _limiteExclusividadePreparacao.Value - cronometro.Elapsed;
                if (restante <= TimeSpan.Zero)
                {
                    throw new TimeoutException(
                        "O limite para preparar o banco local foi excedido.",
                        exception);
                }

                await Task.Delay(
                    restante < IntervaloTentativaTrava ? restante : IntervaloTentativaTrava,
                    cancellationToken);
            }
        }
    }

    private static bool EhViolacaoDeCompartilhamento(IOException exception) =>
        (exception.HResult & 0xFFFF) is 32 or 33;

    private static TimeSpan? ValidarLimiteExclusividade(TimeSpan? limite)
    {
        if (limite is not null &&
            (limite < TimeSpan.Zero || limite.Value.TotalMilliseconds > int.MaxValue))
        {
            throw new ArgumentOutOfRangeException(
                nameof(limite),
                limite,
                "O limite da exclusividade deve ser finito e nao negativo.");
        }

        return limite;
    }
}
