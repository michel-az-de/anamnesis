using System.Globalization;
using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;

namespace Anamnesis.Infrastructure.Observabilidade;

public sealed class SqliteEventoOperacionalRepository :
    IEventoOperacionalSink,
    IEventoOperacionalQuery
{
    private const int TimeoutComandoSegundos = 1;
    private static readonly TimeSpan LimiteExclusividadePreparacao = TimeSpan.FromSeconds(1);
    private readonly BancoLocal _banco;

    public SqliteEventoOperacionalRepository(string caminhoBancoPrincipal)
        : this(caminhoBancoPrincipal, LimiteExclusividadePreparacao)
    {
    }

    internal SqliteEventoOperacionalRepository(
        string caminhoBancoPrincipal,
        TimeSpan limiteExclusividadePreparacao)
    {
        _banco = new BancoLocal(
            ResolverCaminhoJournal(caminhoBancoPrincipal),
            SqliteSchema.InicializarEventosOperacionaisAsync,
            TimeoutComandoSegundos,
            limiteExclusividadePreparacao,
            SqliteSchema.EventosOperacionaisEstaoInicializadosAsync);
    }

    public static string ResolverCaminhoJournal(string caminhoBancoPrincipal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoBancoPrincipal);
        return Path.ChangeExtension(Path.GetFullPath(caminhoBancoPrincipal), "journal.db");
    }

    public async Task RegistrarAsync(
        EventoOperacional evento,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evento);
        await using var conexao = await AbrirAsync(cancellationToken);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            INSERT INTO eventos_operacionais (
                id, criado_em, nivel, codigo, componente, mensagem,
                reuniao_id, job_id, operacao, tentativa, resultado,
                motivo_codigo, duracao_ms)
            VALUES (
                $id, $criadoEm, $nivel, $codigo, $componente, $mensagem,
                $reuniaoId, $jobId, $operacao, $tentativa, $resultado,
                $motivoCodigo, $duracaoMs);
            """;
        comando.Parameters.AddWithValue("$id", evento.Id.ToString("N"));
        comando.Parameters.AddWithValue("$criadoEm", FormatarData(evento.CriadoEm));
        comando.Parameters.AddWithValue("$nivel", evento.Nivel.ToString());
        comando.Parameters.AddWithValue("$codigo", evento.Codigo);
        comando.Parameters.AddWithValue("$componente", evento.Componente);
        comando.Parameters.AddWithValue("$mensagem", evento.Mensagem);
        AdicionarGuidOpcional(comando, "$reuniaoId", evento.ReuniaoId);
        AdicionarGuidOpcional(comando, "$jobId", evento.JobId);
        AdicionarValorOpcional(comando, "$operacao", evento.Metadados.Operacao);
        AdicionarValorOpcional(comando, "$tentativa", evento.Metadados.Tentativa);
        AdicionarValorOpcional(comando, "$resultado", evento.Metadados.Resultado);
        AdicionarValorOpcional(comando, "$motivoCodigo", evento.Metadados.MotivoCodigo);
        AdicionarValorOpcional(comando, "$duracaoMs", evento.Metadados.DuracaoMs);
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RemoverAnterioresAsync(
        DateTimeOffset limiteUtc,
        CancellationToken cancellationToken)
    {
        await using var conexao = await AbrirAsync(cancellationToken);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "DELETE FROM eventos_operacionais WHERE criado_em < $limiteUtc;";
        comando.Parameters.AddWithValue("$limiteUtc", FormatarData(limiteUtc));
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EventoOperacional>> ListarAsync(
        EventoOperacionalFiltro filtro,
        CancellationToken cancellationToken)
    {
        ValidarFiltro(filtro);
        await using var conexao = await AbrirAsync(cancellationToken);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = $$"""
            SELECT id, criado_em, nivel, codigo, componente, mensagem,
                   reuniao_id, job_id, operacao, tentativa, resultado,
                   motivo_codigo, duracao_ms
            FROM eventos_operacionais
            {{CriarWhere()}}
            ORDER BY criado_em DESC, id DESC
            LIMIT $limite;
            """;
        AdicionarFiltros(comando, filtro);
        comando.Parameters.AddWithValue("$limite", filtro.Limite);

        var eventos = new List<EventoOperacional>();
        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
        while (await leitor.ReadAsync(cancellationToken))
        {
            eventos.Add(LerEvento(leitor));
        }

        return eventos;
    }

    public async Task<MetricasOperacionais> ObterMetricasAsync(
        EventoOperacionalFiltro filtro,
        CancellationToken cancellationToken)
    {
        ValidarFiltro(filtro);
        await using var conexao = await AbrirAsync(cancellationToken);
        await using var comando = conexao.CreateCommand();
        comando.CommandText = $$"""
            SELECT COUNT(*),
                   COALESCE(SUM(CASE WHEN nivel IN ('Aviso', 'Erro') THEN 1 ELSE 0 END), 0),
                   COALESCE(AVG(duracao_ms), 0)
            FROM eventos_operacionais
            {{CriarWhere()}};
            """;
        AdicionarFiltros(comando, filtro);

        await using var leitor = await comando.ExecuteReaderAsync(cancellationToken);
        await leitor.ReadAsync(cancellationToken);
        return new MetricasOperacionais(
            leitor.GetInt32(0),
            leitor.GetInt32(1),
            Math.Round(leitor.GetDouble(2), 1));
    }

    private async Task<SqliteConnection> AbrirAsync(CancellationToken cancellationToken)
        => await _banco.AbrirAsync(cancellationToken);

    private static string CriarWhere() => """
        WHERE ($nivel IS NULL OR nivel = $nivel)
          AND ($componente IS NULL OR componente = $componente)
          AND ($codigo IS NULL OR codigo = $codigo)
          AND ($reuniaoId IS NULL OR reuniao_id = $reuniaoId)
          AND ($jobId IS NULL OR job_id = $jobId)
          AND ($inicioUtc IS NULL OR criado_em >= $inicioUtc)
          AND ($fimUtc IS NULL OR criado_em <= $fimUtc)
        """;

    private static void AdicionarFiltros(
        SqliteCommand comando,
        EventoOperacionalFiltro filtro)
    {
        AdicionarValorOpcional(comando, "$nivel", filtro.Nivel?.ToString());
        AdicionarValorOpcional(comando, "$componente", Normalizar(filtro.Componente));
        AdicionarValorOpcional(comando, "$codigo", Normalizar(filtro.Codigo));
        AdicionarGuidOpcional(comando, "$reuniaoId", filtro.ReuniaoId);
        AdicionarGuidOpcional(comando, "$jobId", filtro.JobId);
        AdicionarValorOpcional(
            comando,
            "$inicioUtc",
            filtro.InicioUtc is null ? null : FormatarData(filtro.InicioUtc.Value));
        AdicionarValorOpcional(
            comando,
            "$fimUtc",
            filtro.FimUtc is null ? null : FormatarData(filtro.FimUtc.Value));
    }

    private static EventoOperacional LerEvento(SqliteDataReader leitor) =>
        new(
            Guid.ParseExact(leitor.GetString(0), "N"),
            ParsearData(leitor.GetString(1)),
            Enum.Parse<NivelEventoOperacional>(leitor.GetString(2)),
            leitor.GetString(3),
            leitor.GetString(4),
            leitor.GetString(5),
            LerGuidOpcional(leitor, 6),
            LerGuidOpcional(leitor, 7),
            new MetadadosEventoOperacional(
                leitor.IsDBNull(8) ? null : leitor.GetString(8),
                leitor.IsDBNull(9) ? null : leitor.GetInt32(9),
                leitor.IsDBNull(10) ? null : leitor.GetString(10),
                leitor.IsDBNull(11) ? null : leitor.GetString(11),
                leitor.IsDBNull(12) ? null : leitor.GetDouble(12)));

    private static void ValidarFiltro(EventoOperacionalFiltro filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        if (filtro.Limite is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(filtro), "O limite deve estar entre 1 e 500.");
        }

        if (filtro.InicioUtc is not null &&
            filtro.FimUtc is not null &&
            filtro.InicioUtc > filtro.FimUtc)
        {
            throw new ArgumentException("O início do intervalo deve ser anterior ao fim.", nameof(filtro));
        }
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static void AdicionarGuidOpcional(
        SqliteCommand comando,
        string nome,
        Guid? valor) =>
        AdicionarValorOpcional(comando, nome, valor?.ToString("N"));

    private static void AdicionarValorOpcional(
        SqliteCommand comando,
        string nome,
        object? valor) =>
        comando.Parameters.AddWithValue(nome, valor ?? DBNull.Value);

    private static Guid? LerGuidOpcional(SqliteDataReader leitor, int indice) =>
        leitor.IsDBNull(indice) ? null : Guid.ParseExact(leitor.GetString(indice), "N");

    private static string FormatarData(DateTimeOffset valor) =>
        valor.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParsearData(string valor) =>
        DateTimeOffset.Parse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
