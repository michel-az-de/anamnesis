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
    int? timeoutComandoSegundos = null)
{
    private readonly string _connectionString = CriarConnectionString(
        caminhoBanco,
        timeoutComandoSegundos);

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
}
