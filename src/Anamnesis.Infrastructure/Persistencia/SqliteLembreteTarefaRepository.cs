using System.Globalization;
using Anamnesis.Application.Contracts;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;

namespace Anamnesis.Infrastructure.Persistencia;

public sealed class SqliteLembreteTarefaRepository(string caminhoBanco)
    : ILembreteTarefaRepository
{
    private readonly BancoLocal _banco = new(caminhoBanco, SqliteSchema.InicializarLembretesAsync);

    public async Task SalvarAsync(
        LembreteTarefa lembrete,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lembrete);
        await using var conexao = await _banco.AbrirAsync(cancellationToken);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO lembretes_tarefa (
                id, reuniao_id, descricao_tarefa, lembrar_em, criado_em, status, notificado_em)
            VALUES (
                $id, $reuniaoId, $descricao, $lembrarEm, $criadoEm, $status, $notificadoEm)
            ON CONFLICT(id) DO UPDATE SET
                lembrar_em = excluded.lembrar_em,
                status = excluded.status,
                notificado_em = excluded.notificado_em;
            """;
        comando.Parameters.AddWithValue("$id", lembrete.Id.ToString("N"));
        comando.Parameters.AddWithValue("$reuniaoId", lembrete.ReuniaoId.ToString("N"));
        comando.Parameters.AddWithValue("$descricao", lembrete.DescricaoTarefa);
        comando.Parameters.AddWithValue("$lembrarEm", FormatarData(lembrete.LembrarEm));
        comando.Parameters.AddWithValue("$criadoEm", FormatarData(lembrete.CriadoEm));
        comando.Parameters.AddWithValue("$status", lembrete.Status.ToString());
        comando.Parameters.AddWithValue(
            "$notificadoEm",
            lembrete.NotificadoEm is null ? DBNull.Value : FormatarData(lembrete.NotificadoEm.Value));
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LembreteTarefa>> ListarPendentesAteAsync(
        DateTimeOffset limite,
        CancellationToken cancellationToken)
    {
        await using var conexao = await _banco.AbrirAsync(cancellationToken);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            SELECT id, reuniao_id, descricao_tarefa, lembrar_em, criado_em, status, notificado_em
            FROM lembretes_tarefa
            WHERE status = $status AND lembrar_em <= $limite
            ORDER BY lembrar_em, id;
            """;
        comando.Parameters.AddWithValue("$status", StatusLembreteTarefa.Pendente.ToString());
        comando.Parameters.AddWithValue("$limite", FormatarData(limite));

        var itens = new List<LembreteTarefa>();
        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            itens.Add(LembreteTarefa.Reconstituir(
                Guid.ParseExact(leitor.GetString(0), "N"),
                Guid.ParseExact(leitor.GetString(1), "N"),
                leitor.GetString(2),
                ParsearData(leitor.GetString(3)),
                ParsearData(leitor.GetString(4)),
                Enum.Parse<StatusLembreteTarefa>(leitor.GetString(5)),
                leitor.IsDBNull(6) ? null : ParsearData(leitor.GetString(6))));
        }

        return itens;
    }

    private static string FormatarData(DateTimeOffset valor) =>
        valor.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParsearData(string valor) =>
        DateTimeOffset.Parse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
