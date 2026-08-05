using System.Globalization;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;

namespace Anamnesis.Infrastructure.Fila;

public sealed class SqliteJobQuery(string caminhoBanco) : IJobQuery
{
    private readonly BancoLocal _banco = new(caminhoBanco, SqliteSchema.InicializarJobsAsync);

    public async Task<JobResumo?> ObterMaisRecenteAsync(
        Guid reuniaoId,
        CancellationToken cancellationToken)
    {
        await using var conexao = await _banco.AbrirAsync(cancellationToken);
        await using var consultar = conexao.CreateCommand();
        consultar.CommandText = """
            SELECT id, reuniao_id, criado_em, reservado_em, concluido_em, tentativas
            FROM jobs
            WHERE reuniao_id = $reuniaoId
            ORDER BY criado_em DESC, id DESC
            LIMIT 1;
            """;
        consultar.Parameters.AddWithValue("$reuniaoId", reuniaoId.ToString("N"));

        await using var leitor = await consultar.ExecuteReaderAsync(cancellationToken);
        if (!await leitor.ReadAsync(cancellationToken))
        {
            return null;
        }

        var reservadoEm = LerDataOpcional(leitor, 3);
        var concluidoEm = LerDataOpcional(leitor, 4);
        var estado = concluidoEm is not null
            ? EstadoJobProcessamento.Concluido
            : reservadoEm is not null
                ? EstadoJobProcessamento.EmProcessamento
                : EstadoJobProcessamento.Pendente;
        return new JobResumo(
            Guid.ParseExact(leitor.GetString(0), "N"),
            Guid.ParseExact(leitor.GetString(1), "N"),
            ParsearData(leitor.GetString(2)),
            reservadoEm,
            concluidoEm,
            leitor.GetInt32(5),
            estado);
    }

    private static DateTimeOffset? LerDataOpcional(SqliteDataReader leitor, int indice) =>
        leitor.IsDBNull(indice) ? null : ParsearData(leitor.GetString(indice));

    private static DateTimeOffset ParsearData(string valor) =>
        DateTimeOffset.Parse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}

