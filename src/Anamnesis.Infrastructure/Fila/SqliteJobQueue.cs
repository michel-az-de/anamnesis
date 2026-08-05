using System.Globalization;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;

namespace Anamnesis.Infrastructure.Fila;

public sealed class SqliteJobQueue(string caminhoBanco) : IJobQueue
{
    private readonly BancoLocal _banco = new(caminhoBanco, SqliteSchema.InicializarJobsAsync);

    public async Task<JobProcessamento> EnfileirarAsync(
        Guid reuniaoId,
        DateTimeOffset criadoEm,
        CancellationToken cancellationToken)
    {
        await using var conexao = await _banco.AbrirAsync(cancellationToken);
        await using var transacao = conexao.BeginTransaction(deferred: false);

        await using (var inserir = conexao.CreateCommand())
        {
            inserir.Transaction = transacao;
            inserir.CommandText = """
                INSERT OR IGNORE INTO jobs (id, reuniao_id, criado_em, tentativas)
                VALUES ($id, $reuniaoId, $criadoEm, 0);
                """;
            inserir.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
            inserir.Parameters.AddWithValue("$reuniaoId", reuniaoId.ToString("N"));
            inserir.Parameters.AddWithValue("$criadoEm", FormatarData(criadoEm));
            await inserir.ExecuteNonQueryAsync(cancellationToken);
        }

        JobProcessamento job;
        await using (var consultar = conexao.CreateCommand())
        {
            consultar.Transaction = transacao;
            consultar.CommandText = """
                SELECT id, reuniao_id, criado_em, reservado_em, tentativas
                FROM jobs
                WHERE reuniao_id = $reuniaoId AND concluido_em IS NULL
                ORDER BY criado_em, id
                LIMIT 1;
                """;
            consultar.Parameters.AddWithValue("$reuniaoId", reuniaoId.ToString("N"));

            await using var leitor = await consultar.ExecuteReaderAsync(cancellationToken);
            if (!await leitor.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Não foi possível enfileirar o job de processamento.");
            }

            job = LerJob(leitor);
        }

        await transacao.CommitAsync(cancellationToken);
        return job;
    }

    public async Task<JobProcessamento?> ReservarProximoAsync(
        DateTimeOffset reservadoEm,
        CancellationToken cancellationToken)
    {
        await using var conexao = await _banco.AbrirAsync(cancellationToken);
        await using var reservar = conexao.CreateCommand();
        reservar.CommandText = """
            UPDATE jobs
            SET reservado_em = $reservadoEm,
                tentativas = tentativas + 1
            WHERE id = (
                SELECT id
                FROM jobs
                WHERE reservado_em IS NULL AND concluido_em IS NULL
                ORDER BY criado_em, id
                LIMIT 1
            )
            AND reservado_em IS NULL
            AND concluido_em IS NULL
            RETURNING id, reuniao_id, criado_em, reservado_em, tentativas;
            """;
        reservar.Parameters.AddWithValue("$reservadoEm", FormatarData(reservadoEm));

        await using var leitor = await reservar.ExecuteReaderAsync(cancellationToken);
        return await leitor.ReadAsync(cancellationToken) ? LerJob(leitor) : null;
    }

    public async Task LiberarAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var conexao = await _banco.AbrirAsync(cancellationToken);
        await using var liberar = conexao.CreateCommand();
        liberar.CommandText = """
            UPDATE jobs
            SET reservado_em = NULL
            WHERE id = $jobId AND concluido_em IS NULL;
            """;
        liberar.Parameters.AddWithValue("$jobId", jobId.ToString("N"));
        await liberar.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task LiberarReservasAtivasAsync(CancellationToken cancellationToken)
    {
        await using var conexao = await _banco.AbrirAsync(cancellationToken);
        await using var liberar = conexao.CreateCommand();

        // Liberar tudo só é correto porque quem chama detém a exclusividade da instância
        // do Worker (ADR-012): nenhuma destas reservas pode pertencer a um Worker vivo.
        liberar.CommandText = """
            UPDATE jobs
            SET reservado_em = NULL
            WHERE reservado_em IS NOT NULL AND concluido_em IS NULL;
            """;
        await liberar.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ConcluirAsync(Guid jobId, DateTimeOffset concluidoEm, CancellationToken cancellationToken)
    {
        await using var conexao = await _banco.AbrirAsync(cancellationToken);
        await using var concluir = conexao.CreateCommand();
        concluir.CommandText = """
            UPDATE jobs
            SET concluido_em = $concluidoEm
            WHERE id = $jobId AND concluido_em IS NULL;
            """;
        concluir.Parameters.AddWithValue("$jobId", jobId.ToString("N"));
        concluir.Parameters.AddWithValue("$concluidoEm", FormatarData(concluidoEm));
        await concluir.ExecuteNonQueryAsync(cancellationToken);
    }

    private static JobProcessamento LerJob(SqliteDataReader leitor) => new(
        Guid.ParseExact(leitor.GetString(0), "N"),
        Guid.ParseExact(leitor.GetString(1), "N"),
        ParsearData(leitor.GetString(2)),
        leitor.IsDBNull(3) ? null : ParsearData(leitor.GetString(3)),
        leitor.GetInt32(4));

    private static string FormatarData(DateTimeOffset valor) =>
        valor.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParsearData(string valor) =>
        DateTimeOffset.Parse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
