using System.Globalization;
using System.Text.Json;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.UseCases;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Microsoft.Data.Sqlite;

namespace Anamnesis.Infrastructure.Persistencia;

public sealed class SqliteReuniaoRepository(string caminhoBanco) : IReuniaoRepository
{
    private readonly BancoLocal _banco = new(caminhoBanco, SqliteSchema.InicializarReunioesAsync);

    internal int PreparacoesDeEsquema => _banco.Preparacoes;

    public async Task<Reuniao?> ObterAsync(Guid reuniaoId, CancellationToken cancellationToken)
    {
        await using var conexao = await _banco.AbrirAsync(cancellationToken);
        await using var consultar = conexao.CreateCommand();
        consultar.CommandText = """
            SELECT id, titulo, criada_em, status, motivo_falha,
                   gravacao_caminho, gravacao_iniciada_em, gravacao_finalizada_em,
                   transcricao_texto, transcricao_idioma, transcricao_gerada_em,
                   ata_resumo_executivo, ata_decisoes_json, ata_tarefas_json, ata_gerada_em,
                   arquivada_em
            FROM reunioes
            WHERE id = $id;
            """;
        consultar.Parameters.AddWithValue("$id", reuniaoId.ToString("N"));

        await using var leitor = await consultar.ExecuteReaderAsync(cancellationToken);
        return await leitor.ReadAsync(cancellationToken) ? LerReuniao(leitor) : null;
    }

    public async Task SalvarAsync(Reuniao reuniao, CancellationToken cancellationToken)
    {
        await using var conexao = await _banco.AbrirAsync(cancellationToken);
        await using var salvar = conexao.CreateCommand();
        salvar.CommandText = """
            INSERT INTO reunioes (
                id, titulo, criada_em, status, motivo_falha,
                gravacao_caminho, gravacao_iniciada_em, gravacao_finalizada_em,
                transcricao_texto, transcricao_idioma, transcricao_gerada_em,
                ata_resumo_executivo, ata_decisoes_json, ata_tarefas_json, ata_gerada_em,
                arquivada_em)
            VALUES (
                $id, $titulo, $criadaEm, $status, $motivoFalha,
                $gravacaoCaminho, $gravacaoIniciadaEm, $gravacaoFinalizadaEm,
                $transcricaoTexto, $transcricaoIdioma, $transcricaoGeradaEm,
                $ataResumoExecutivo, $ataDecisoesJson, $ataTarefasJson, $ataGeradaEm,
                $arquivadaEm)
            ON CONFLICT(id) DO UPDATE SET
                titulo = excluded.titulo,
                criada_em = excluded.criada_em,
                status = excluded.status,
                motivo_falha = excluded.motivo_falha,
                gravacao_caminho = excluded.gravacao_caminho,
                gravacao_iniciada_em = excluded.gravacao_iniciada_em,
                gravacao_finalizada_em = excluded.gravacao_finalizada_em,
                transcricao_texto = excluded.transcricao_texto,
                transcricao_idioma = excluded.transcricao_idioma,
                transcricao_gerada_em = excluded.transcricao_gerada_em,
                ata_resumo_executivo = excluded.ata_resumo_executivo,
                ata_decisoes_json = excluded.ata_decisoes_json,
                ata_tarefas_json = excluded.ata_tarefas_json,
                ata_gerada_em = excluded.ata_gerada_em,
                arquivada_em = excluded.arquivada_em;
            """;

        salvar.Parameters.AddWithValue("$id", reuniao.Id.ToString("N"));
        salvar.Parameters.AddWithValue("$titulo", reuniao.Titulo);
        salvar.Parameters.AddWithValue("$criadaEm", FormatarData(reuniao.CriadaEm));
        salvar.Parameters.AddWithValue("$status", reuniao.Status.ToString());
        AdicionarValorOpcional(salvar, "$motivoFalha", reuniao.MotivoFalha);
        AdicionarValorOpcional(salvar, "$gravacaoCaminho", reuniao.Gravacao?.CaminhoArquivo);
        AdicionarDataOpcional(salvar, "$gravacaoIniciadaEm", reuniao.Gravacao?.IniciadaEm);
        AdicionarDataOpcional(salvar, "$gravacaoFinalizadaEm", reuniao.Gravacao?.FinalizadaEm);
        AdicionarValorOpcional(salvar, "$transcricaoTexto", reuniao.Transcricao?.Texto);
        AdicionarValorOpcional(salvar, "$transcricaoIdioma", reuniao.Transcricao?.Idioma);
        AdicionarDataOpcional(salvar, "$transcricaoGeradaEm", reuniao.Transcricao?.GeradaEm);
        AdicionarValorOpcional(salvar, "$ataResumoExecutivo", reuniao.Ata?.ResumoExecutivo);
        AdicionarValorOpcional(salvar, "$ataDecisoesJson", reuniao.Ata is null ? null : JsonSerializer.Serialize(reuniao.Ata.Decisoes));
        AdicionarValorOpcional(salvar, "$ataTarefasJson", reuniao.Ata is null ? null : JsonSerializer.Serialize(reuniao.Ata.Tarefas));
        AdicionarDataOpcional(salvar, "$ataGeradaEm", reuniao.Ata?.GeradaEm);
        AdicionarDataOpcional(salvar, "$arquivadaEm", reuniao.ArquivadaEm);
        try
        {
            await salvar.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException exception) when (
            exception.SqliteErrorCode == 19 &&
            exception.SqliteExtendedErrorCode == 2067)
        {
            throw new GravacaoJaAtivaException(exception);
        }
    }

    private static Reuniao LerReuniao(SqliteDataReader leitor)
    {
        var gravacao = leitor.IsDBNull(5)
            ? null
            : new Gravacao(leitor.GetString(5), ParsearData(leitor.GetString(6)), ParsearData(leitor.GetString(7)));
        var transcricao = leitor.IsDBNull(8)
            ? null
            : new Transcricao(leitor.GetString(8), leitor.GetString(9), ParsearData(leitor.GetString(10)));
        var ata = leitor.IsDBNull(11)
            ? null
            : new Ata(
                leitor.GetString(11),
                JsonSerializer.Deserialize<List<string>>(leitor.GetString(12)) ?? [],
                JsonSerializer.Deserialize<List<Tarefa>>(leitor.GetString(13)) ?? [],
                ParsearData(leitor.GetString(14)));

        return Reuniao.Reconstituir(
            Guid.ParseExact(leitor.GetString(0), "N"),
            leitor.GetString(1),
            ParsearData(leitor.GetString(2)),
            Enum.Parse<StatusReuniao>(leitor.GetString(3)),
            gravacao,
            transcricao,
            ata,
            leitor.IsDBNull(4) ? null : leitor.GetString(4),
            leitor.IsDBNull(15) ? null : ParsearData(leitor.GetString(15)));
    }

    private static void AdicionarValorOpcional(SqliteCommand comando, string nome, string? valor) =>
        comando.Parameters.AddWithValue(nome, valor ?? (object)DBNull.Value);

    private static void AdicionarDataOpcional(SqliteCommand comando, string nome, DateTimeOffset? valor) =>
        AdicionarValorOpcional(comando, nome, valor is null ? null : FormatarData(valor.Value));

    private static string FormatarData(DateTimeOffset valor) =>
        valor.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParsearData(string valor) =>
        DateTimeOffset.Parse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
