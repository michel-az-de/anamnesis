using System.Globalization;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Microsoft.Data.Sqlite;

namespace Anamnesis.Infrastructure.Persistencia;

public sealed class SqliteArtefatoRepository(string caminhoBanco, TimeProvider relogio)
    : IArtefatoRepository
{
    private readonly BancoLocal _banco = new(caminhoBanco, SqliteSchema.InicializarArtefatosAsync);

    public SqliteArtefatoRepository(string caminhoBanco)
        : this(caminhoBanco, TimeProvider.System)
    {
    }

    public async Task SalvarAsync(
        ArtefatosReuniao artefatos,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artefatos);
        ValidarCaminho(artefatos.Diretorio, nameof(artefatos.Diretorio));
        ValidarCaminho(artefatos.CaminhoAta, nameof(artefatos.CaminhoAta));
        ValidarCaminho(artefatos.CaminhoTranscricao, nameof(artefatos.CaminhoTranscricao));

        await using var conexao = await _banco.AbrirAsync(cancellationToken);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO reuniao_artefatos (
                reuniao_id, diretorio, caminho_ata, caminho_transcricao, arquivado_em)
            VALUES ($reuniao_id, $diretorio, $caminho_ata, $caminho_transcricao, $arquivado_em)
            ON CONFLICT(reuniao_id) DO UPDATE SET
                diretorio = excluded.diretorio,
                caminho_ata = excluded.caminho_ata,
                caminho_transcricao = excluded.caminho_transcricao,
                arquivado_em = excluded.arquivado_em;
            """;
        comando.Parameters.AddWithValue("$reuniao_id", artefatos.ReuniaoId.ToString("N"));
        comando.Parameters.AddWithValue("$diretorio", artefatos.Diretorio);
        comando.Parameters.AddWithValue("$caminho_ata", artefatos.CaminhoAta);
        comando.Parameters.AddWithValue("$caminho_transcricao", artefatos.CaminhoTranscricao);
        comando.Parameters.AddWithValue(
            "$arquivado_em",
            relogio.GetUtcNow().ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ArtefatosReuniao?> ObterAsync(
        Guid reuniaoId,
        CancellationToken cancellationToken)
    {
        await using var conexao = await _banco.AbrirAsync(cancellationToken);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            SELECT diretorio, caminho_ata, caminho_transcricao
            FROM reuniao_artefatos
            WHERE reuniao_id = $reuniao_id;
            """;
        comando.Parameters.AddWithValue("$reuniao_id", reuniaoId.ToString("N"));

        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
        return await leitor.ReadAsync(cancellationToken)
            ? new ArtefatosReuniao(
                reuniaoId,
                leitor.GetString(0),
                leitor.GetString(1),
                leitor.GetString(2))
            : null;
    }

    private static void ValidarCaminho(string caminho, string nomeParametro)
    {
        if (string.IsNullOrWhiteSpace(caminho))
        {
            throw new ArgumentException("O caminho do artefato deve ser informado.", nomeParametro);
        }
    }
}
