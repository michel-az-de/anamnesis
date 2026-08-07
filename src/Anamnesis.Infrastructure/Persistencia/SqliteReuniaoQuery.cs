using System.Globalization;
using System.Text.Json;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Microsoft.Data.Sqlite;

namespace Anamnesis.Infrastructure.Persistencia;

public sealed class SqliteReuniaoQuery(string caminhoBanco) : IReuniaoQuery
{
    private readonly BancoLocal _banco = new(caminhoBanco, async (conexao, cancellationToken) =>
    {
        await SqliteSchema.InicializarReunioesAsync(conexao, cancellationToken);
        await SqliteSchema.InicializarArtefatosAsync(conexao, cancellationToken);
    });

    public async Task<IReadOnlyList<ReuniaoResumo>> ListarAsync(
        ReuniaoQueryFiltro filtro,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        if (filtro.Limite is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(filtro), "O limite deve estar entre 1 e 100.");
        }

        await using var conexao = await _banco.AbrirAsync(cancellationToken);
        await using var consultar = conexao.CreateCommand();
        consultar.CommandText = """
            SELECT id, titulo, criada_em, status,
                   gravacao_iniciada_em, gravacao_finalizada_em, motivo_falha,
                   CASE
                       WHEN $texto IS NULL THEN NULL
                       WHEN titulo LIKE $padrao ESCAPE '\' COLLATE NOCASE THEN 'Resumo'
                       WHEN ata_resumo_executivo LIKE $padrao ESCAPE '\' COLLATE NOCASE THEN 'Resumo'
                       WHEN ata_decisoes_json LIKE $padrao ESCAPE '\' COLLATE NOCASE THEN 'Decisões'
                       WHEN ata_tarefas_json LIKE $padrao ESCAPE '\' COLLATE NOCASE THEN 'Tarefas'
                       WHEN transcricao_texto LIKE $padrao ESCAPE '\' COLLATE NOCASE THEN 'Transcrição'
                       ELSE NULL
                   END AS secao_correspondente,
                   CASE
                       WHEN $texto IS NULL THEN NULL
                       WHEN titulo LIKE $padrao ESCAPE '\' COLLATE NOCASE THEN titulo
                       WHEN ata_resumo_executivo LIKE $padrao ESCAPE '\' COLLATE NOCASE THEN ata_resumo_executivo
                       WHEN ata_decisoes_json LIKE $padrao ESCAPE '\' COLLATE NOCASE THEN ata_decisoes_json
                       WHEN ata_tarefas_json LIKE $padrao ESCAPE '\' COLLATE NOCASE THEN ata_tarefas_json
                       WHEN transcricao_texto LIKE $padrao ESCAPE '\' COLLATE NOCASE THEN transcricao_texto
                       ELSE NULL
                   END AS origem_trecho
            FROM reunioes
            WHERE ($texto IS NULL
                   OR titulo LIKE $padrao ESCAPE '\' COLLATE NOCASE
                   OR ata_resumo_executivo LIKE $padrao ESCAPE '\' COLLATE NOCASE
                   OR ata_decisoes_json LIKE $padrao ESCAPE '\' COLLATE NOCASE
                   OR ata_tarefas_json LIKE $padrao ESCAPE '\' COLLATE NOCASE
                   OR transcricao_texto LIKE $padrao ESCAPE '\' COLLATE NOCASE)
              AND ($status IS NULL OR status = $status)
              AND ($criada_desde IS NULL OR criada_em >= $criada_desde)
            ORDER BY criada_em DESC, id DESC
            LIMIT $limite;
            """;
        var texto = NormalizarTexto(filtro.Texto);
        AdicionarValorOpcional(consultar, "$texto", texto);
        AdicionarValorOpcional(consultar, "$padrao", CriarPadraoBusca(texto));
        AdicionarValorOpcional(consultar, "$status", filtro.Status?.ToString());
        AdicionarValorOpcional(
            consultar,
            "$criada_desde",
            filtro.CriadaDesde is null ? null : FormatarData(filtro.CriadaDesde.Value));
        consultar.Parameters.AddWithValue("$limite", filtro.Limite);

        var resultado = new List<ReuniaoResumo>();
        await using var leitor = await consultar.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            resultado.Add(new ReuniaoResumo(
                Guid.ParseExact(leitor.GetString(0), "N"),
                leitor.GetString(1),
                ParsearData(leitor.GetString(2)),
                Enum.Parse<StatusReuniao>(leitor.GetString(3)),
                LerDataOpcional(leitor, 4),
                LerDataOpcional(leitor, 5),
                leitor.IsDBNull(6) ? null : leitor.GetString(6),
                leitor.IsDBNull(7) ? null : leitor.GetString(7),
                leitor.IsDBNull(8) ? null : CriarTrecho(leitor.GetString(8), texto)));
        }

        return resultado;
    }

    public async Task<ReuniaoDetalhe?> ObterDetalheAsync(
        Guid reuniaoId,
        CancellationToken cancellationToken)
    {
        await using var conexao = await _banco.AbrirAsync(cancellationToken);
        await using var consultar = conexao.CreateCommand();
        consultar.CommandText = """
            SELECT r.id, r.titulo, r.criada_em, r.status, r.motivo_falha,
                   r.gravacao_caminho, r.gravacao_iniciada_em, r.gravacao_finalizada_em,
                   r.transcricao_texto, r.transcricao_idioma, r.transcricao_gerada_em,
                   r.ata_resumo_executivo, r.ata_decisoes_json, r.ata_tarefas_json,
                   r.ata_gerada_em, r.arquivada_em,
                   a.diretorio, a.caminho_ata, a.caminho_transcricao
            FROM reunioes r
            LEFT JOIN reuniao_artefatos a ON a.reuniao_id = r.id
            WHERE r.id = $id;
            """;
        consultar.Parameters.AddWithValue("$id", reuniaoId.ToString("N"));

        await using var leitor = await consultar.ExecuteReaderAsync(cancellationToken);
        return await leitor.ReadAsync(cancellationToken) ? LerDetalhe(leitor) : null;
    }

    private static ReuniaoDetalhe LerDetalhe(SqliteDataReader leitor)
    {
        var reuniaoId = Guid.ParseExact(leitor.GetString(0), "N");
        var artefatos = leitor.IsDBNull(16)
            ? null
            : new ArtefatosReuniao(
                reuniaoId,
                leitor.GetString(16),
                leitor.GetString(17),
                leitor.GetString(18));

        return new ReuniaoDetalhe(
            reuniaoId,
            leitor.GetString(1),
            ParsearData(leitor.GetString(2)),
            Enum.Parse<StatusReuniao>(leitor.GetString(3)),
            leitor.IsDBNull(4) ? null : leitor.GetString(4),
            leitor.IsDBNull(5) ? null : leitor.GetString(5),
            LerDataOpcional(leitor, 6),
            LerDataOpcional(leitor, 7),
            leitor.IsDBNull(8) ? null : leitor.GetString(8),
            leitor.IsDBNull(9) ? null : leitor.GetString(9),
            LerDataOpcional(leitor, 10),
            leitor.IsDBNull(11) ? null : leitor.GetString(11),
            leitor.IsDBNull(12)
                ? []
                : JsonSerializer.Deserialize<List<string>>(leitor.GetString(12)) ?? [],
            leitor.IsDBNull(13)
                ? []
                : JsonSerializer.Deserialize<List<Tarefa>>(leitor.GetString(13)) ?? [],
            LerDataOpcional(leitor, 14),
            LerDataOpcional(leitor, 15),
            artefatos);
    }

    private static string? NormalizarTexto(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static string? CriarPadraoBusca(string? texto) =>
        texto is null
            ? null
            : $"%{texto.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";

    private static string CriarTrecho(string origem, string? texto)
    {
        var normalizado = string.Join(
            ' ',
            origem.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (normalizado.Length <= 180)
        {
            return normalizado;
        }

        var indice = texto is null
            ? 0
            : normalizado.IndexOf(texto, StringComparison.OrdinalIgnoreCase);
        var inicio = Math.Max(0, indice < 0 ? 0 : indice - 55);
        var comprimento = Math.Min(170, normalizado.Length - inicio);
        var trecho = normalizado.Substring(inicio, comprimento).Trim();
        return $"{(inicio > 0 ? "..." : string.Empty)}{trecho}{(inicio + comprimento < normalizado.Length ? "..." : string.Empty)}";
    }

    private static void AdicionarValorOpcional(SqliteCommand comando, string nome, string? valor) =>
        comando.Parameters.AddWithValue(nome, valor ?? (object)DBNull.Value);

    private static DateTimeOffset? LerDataOpcional(SqliteDataReader leitor, int indice) =>
        leitor.IsDBNull(indice) ? null : ParsearData(leitor.GetString(indice));

    private static DateTimeOffset ParsearData(string valor) =>
        DateTimeOffset.Parse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string FormatarData(DateTimeOffset valor) =>
        valor.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
