using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Anamnesis.Infrastructure.Persistencia;

internal static class SqliteSchema
{
    public static async Task InicializarReunioesAsync(
        SqliteConnection conexao,
        CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            CREATE TABLE IF NOT EXISTS reunioes (
                id TEXT NOT NULL PRIMARY KEY,
                titulo TEXT NOT NULL,
                criada_em TEXT NOT NULL,
                status TEXT NOT NULL,
                motivo_falha TEXT NULL,
                gravacao_caminho TEXT NULL,
                gravacao_iniciada_em TEXT NULL,
                gravacao_finalizada_em TEXT NULL,
                transcricao_texto TEXT NULL,
                transcricao_idioma TEXT NULL,
                transcricao_gerada_em TEXT NULL,
                ata_resumo_executivo TEXT NULL,
                ata_decisoes_json TEXT NULL,
                ata_tarefas_json TEXT NULL,
                ata_gerada_em TEXT NULL,
                arquivada_em TEXT NULL
            );
            """;
        await comando.ExecuteNonQueryAsync(cancellationToken);

        await using var verificarColuna = conexao.CreateCommand();
        verificarColuna.CommandText = "SELECT COUNT(*) FROM pragma_table_info('reunioes') WHERE name = 'arquivada_em';";
        var colunaExiste = Convert.ToInt32(
            await verificarColuna.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture) > 0;
        if (!colunaExiste)
        {
            await using var adicionarColuna = conexao.CreateCommand();
            adicionarColuna.CommandText = "ALTER TABLE reunioes ADD COLUMN arquivada_em TEXT NULL;";
            await adicionarColuna.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var reconciliarDuplicatas = conexao.CreateCommand();
        reconciliarDuplicatas.CommandText = """
            UPDATE reunioes
            SET status = 'Falha',
                motivo_falha = 'Gravação ativa duplicada reconciliada durante migração de banco legado.'
            WHERE status = 'Gravando'
              AND id <> (
                  SELECT id
                  FROM reunioes
                  WHERE status = 'Gravando'
                  ORDER BY criada_em DESC, id DESC
                  LIMIT 1
              );
            """;
        await reconciliarDuplicatas.ExecuteNonQueryAsync(cancellationToken);

        await using var indice = conexao.CreateCommand();
        indice.CommandText = """
            CREATE UNIQUE INDEX IF NOT EXISTS ux_reunioes_gravando
            ON reunioes(status)
            WHERE status = 'Gravando';
            """;
        await indice.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task InicializarJobsAsync(
        SqliteConnection conexao,
        CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            CREATE TABLE IF NOT EXISTS jobs (
                id TEXT NOT NULL PRIMARY KEY,
                reuniao_id TEXT NOT NULL,
                criado_em TEXT NOT NULL,
                reservado_em TEXT NULL,
                concluido_em TEXT NULL,
                tentativas INTEGER NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_jobs_reuniao_ativa
            ON jobs(reuniao_id)
            WHERE concluido_em IS NULL;
            """;
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task InicializarArtefatosAsync(
        SqliteConnection conexao,
        CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            CREATE TABLE IF NOT EXISTS reuniao_artefatos (
                reuniao_id TEXT NOT NULL PRIMARY KEY,
                diretorio TEXT NOT NULL,
                caminho_ata TEXT NOT NULL,
                caminho_transcricao TEXT NOT NULL,
                arquivado_em TEXT NOT NULL
            );
            """;
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task InicializarLembretesAsync(
        SqliteConnection conexao,
        CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            CREATE TABLE IF NOT EXISTS lembretes_tarefa (
                id TEXT NOT NULL PRIMARY KEY,
                reuniao_id TEXT NOT NULL,
                descricao_tarefa TEXT NOT NULL,
                lembrar_em TEXT NOT NULL,
                criado_em TEXT NOT NULL,
                status TEXT NOT NULL,
                notificado_em TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_lembretes_tarefa_pendentes
            ON lembretes_tarefa(status, lembrar_em);
            """;
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task InicializarEventosOperacionaisAsync(
        SqliteConnection conexao,
        CancellationToken cancellationToken)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = """
            CREATE TABLE IF NOT EXISTS eventos_operacionais (
                id TEXT NOT NULL PRIMARY KEY,
                criado_em TEXT NOT NULL,
                nivel TEXT NOT NULL,
                codigo TEXT NOT NULL,
                componente TEXT NOT NULL,
                mensagem TEXT NOT NULL,
                reuniao_id TEXT NULL,
                job_id TEXT NULL,
                operacao TEXT NULL,
                tentativa INTEGER NULL,
                resultado TEXT NULL,
                motivo_codigo TEXT NULL,
                duracao_ms REAL NULL
            );

            CREATE INDEX IF NOT EXISTS ix_eventos_operacionais_criado
            ON eventos_operacionais(criado_em DESC, id DESC);

            CREATE INDEX IF NOT EXISTS ix_eventos_operacionais_filtros
            ON eventos_operacionais(nivel, componente, codigo);

            CREATE INDEX IF NOT EXISTS ix_eventos_operacionais_reuniao
            ON eventos_operacionais(reuniao_id, criado_em DESC);

            CREATE INDEX IF NOT EXISTS ix_eventos_operacionais_job
            ON eventos_operacionais(job_id, criado_em DESC);
            """;
        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<bool> EventosOperacionaisEstaoInicializadosAsync(
        SqliteConnection conexao,
        CancellationToken cancellationToken)
    {
        await using (var consultarModo = conexao.CreateCommand())
        {
            consultarModo.CommandText = "PRAGMA journal_mode;";
            var modo = Convert.ToString(
                await consultarModo.ExecuteScalarAsync(cancellationToken),
                CultureInfo.InvariantCulture);
            if (!string.Equals(modo, "wal", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        await using var consultarSchema = conexao.CreateCommand();
        consultarSchema.CommandText = """
            SELECT
                (SELECT COUNT(*)
                 FROM pragma_table_info('eventos_operacionais')
                 WHERE name IN (
                     'id', 'criado_em', 'nivel', 'codigo', 'componente', 'mensagem',
                     'reuniao_id', 'job_id', 'operacao', 'tentativa', 'resultado',
                     'motivo_codigo', 'duracao_ms')) = 13
                AND
                (SELECT COUNT(*)
                 FROM sqlite_master
                 WHERE type = 'index'
                   AND name IN (
                       'ix_eventos_operacionais_criado',
                       'ix_eventos_operacionais_filtros',
                       'ix_eventos_operacionais_reuniao',
                       'ix_eventos_operacionais_job')) = 4;
            """;
        return Convert.ToInt32(
            await consultarSchema.ExecuteScalarAsync(cancellationToken),
            CultureInfo.InvariantCulture) == 1;
    }
}
