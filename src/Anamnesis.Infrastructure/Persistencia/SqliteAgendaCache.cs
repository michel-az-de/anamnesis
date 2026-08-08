using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Anamnesis.Application.Contracts;
using Anamnesis.Domain.Entidades;
using Microsoft.Data.Sqlite;

namespace Anamnesis.Infrastructure.Persistencia;

public class SqliteAgendaCache : IAgendaCache
{
    private readonly string _connectionString;

    public SqliteAgendaCache(string caminhoDb)
    {
        _connectionString = $"Data Source={caminhoDb}";
        InicializarEsquemaAsync().Wait();
    }

    public SqliteAgendaCache(string caminhoDb, bool pooling)
    {
        _connectionString = $"Data Source={caminhoDb};Pooling={pooling.ToString().ToLowerInvariant()}";
        InicializarEsquemaAsync().Wait();
    }

    private async Task InicializarEsquemaAsync()
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ContaAgenda (
                ContaAgendaId TEXT PRIMARY KEY,
                Provider TEXT NOT NULL,
                Estado TEXT NOT NULL,
                CursorSync TEXT,
                JanelaSyncInicio TEXT,
                JanelaSyncFim TEXT,
                AtualizadoEm TEXT
            );

            CREATE TABLE IF NOT EXISTS EventoAgenda (
                EventoAgendaId TEXT PRIMARY KEY,
                ContaAgendaId TEXT NOT NULL REFERENCES ContaAgenda(ContaAgendaId) ON DELETE CASCADE,
                EventoExternoId TEXT NOT NULL,
                Titulo TEXT,
                Inicio TEXT NOT NULL,
                Fim TEXT NOT NULL,
                FusoOriginal TEXT,
                UrlReuniao TEXT,
                Status TEXT,
                AtualizadoEm TEXT,
                UNIQUE (ContaAgendaId, EventoExternoId)
            );

            CREATE INDEX IF NOT EXISTS IX_EventoAgenda_Conta ON EventoAgenda(ContaAgendaId);
            CREATE INDEX IF NOT EXISTS IX_EventoAgenda_Inicio ON EventoAgenda(Inicio);
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SalvarContaAsync(ContaAgenda conta, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ContaAgenda (ContaAgendaId, Provider, Estado, CursorSync, JanelaSyncInicio, JanelaSyncFim, AtualizadoEm)
            VALUES (@id, @provider, @estado, @cursor, @janelaInicio, @janelaFim, @atualizado)
            ON CONFLICT(ContaAgendaId) DO UPDATE SET
                Provider = excluded.Provider,
                Estado = excluded.Estado,
                CursorSync = excluded.CursorSync,
                JanelaSyncInicio = excluded.JanelaSyncInicio,
                JanelaSyncFim = excluded.JanelaSyncFim,
                AtualizadoEm = excluded.AtualizadoEm;
        ";
        cmd.Parameters.AddWithValue("@id", conta.ContaAgendaId);
        cmd.Parameters.AddWithValue("@provider", conta.Provider);
        cmd.Parameters.AddWithValue("@estado", conta.Estado);
        cmd.Parameters.AddWithValue("@cursor", conta.CursorSync ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@janelaInicio", conta.JanelaSyncInicio ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@janelaFim", conta.JanelaSyncFim ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@atualizado", conta.AtualizadoEm ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<ContaAgenda?> ObterContaAsync(string contaAgendaId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ContaAgenda WHERE ContaAgendaId = @id";
        cmd.Parameters.AddWithValue("@id", contaAgendaId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        return MapearConta(reader);
    }

    public async Task<IReadOnlyList<ContaAgenda>> ListarContasAsync(CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM ContaAgenda";

        var contas = new List<ContaAgenda>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            contas.Add(MapearConta(reader));
        }
        return contas;
    }

    public async Task RemoverContaAsync(string contaAgendaId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ContaAgenda WHERE ContaAgendaId = @id";
        cmd.Parameters.AddWithValue("@id", contaAgendaId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SalvarEventosAsync(string contaAgendaId, IEnumerable<EventoAgenda> eventos, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var transaction = await conn.BeginTransactionAsync(ct);
        try
        {
            await using (var clearCmd = conn.CreateCommand())
            {
                clearCmd.Transaction = (SqliteTransaction)transaction;
                clearCmd.CommandText = "DELETE FROM EventoAgenda WHERE ContaAgendaId = @contaId";
                clearCmd.Parameters.AddWithValue("@contaId", contaAgendaId);
                await clearCmd.ExecuteNonQueryAsync(ct);
            }

            foreach (var evt in eventos)
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = (SqliteTransaction)transaction;
                cmd.CommandText = @"
                    INSERT INTO EventoAgenda (EventoAgendaId, ContaAgendaId, EventoExternoId, Titulo, Inicio, Fim, FusoOriginal, UrlReuniao, Status, AtualizadoEm)
                    VALUES (@id, @contaId, @externoId, @titulo, @inicio, @fim, @fuso, @url, @status, @atualizado);
                ";
                cmd.Parameters.AddWithValue("@id", evt.EventoAgendaId);
                cmd.Parameters.AddWithValue("@contaId", contaAgendaId);
                cmd.Parameters.AddWithValue("@externoId", evt.EventoExternoId);
                cmd.Parameters.AddWithValue("@titulo", evt.Titulo ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@inicio", evt.Inicio);
                cmd.Parameters.AddWithValue("@fim", evt.Fim);
                cmd.Parameters.AddWithValue("@fuso", evt.FusoOriginal ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@url", evt.UrlReuniao ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@status", evt.Status ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@atualizado", evt.AtualizadoEm ?? (object)DBNull.Value);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<EventoAgenda>> ListarEventosAsync(string contaAgendaId, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM EventoAgenda WHERE ContaAgendaId = @contaId ORDER BY Inicio";
        cmd.Parameters.AddWithValue("@contaId", contaAgendaId);

        var eventos = new List<EventoAgenda>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            eventos.Add(MapearEvento(reader));
        }
        return eventos;
    }

    public async Task<IReadOnlyList<EventoAgenda>> ListarEventosProximosAsync(int minutos = 30, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var limite = agora.AddMinutes(minutos);
        var agoraIso = agora.ToString("O");
        var limiteIso = limite.ToString("O");

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT * FROM EventoAgenda 
            WHERE Inicio >= @agora AND Inicio <= @limite AND (Status IS NULL OR Status != 'Cancelado')
            ORDER BY Inicio;
        ";
        cmd.Parameters.AddWithValue("@agora", agoraIso);
        cmd.Parameters.AddWithValue("@limite", limiteIso);

        var eventos = new List<EventoAgenda>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            eventos.Add(MapearEvento(reader));
        }
        return eventos;
    }

    public async Task RemoverEventosForaDaJanelaAsync(string contaAgendaId, string inicio, string fim, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM EventoAgenda 
            WHERE ContaAgendaId = @contaId 
            AND (Fim < @inicio OR Inicio > @fim);
        ";
        cmd.Parameters.AddWithValue("@contaId", contaAgendaId);
        cmd.Parameters.AddWithValue("@inicio", inicio);
        cmd.Parameters.AddWithValue("@fim", fim);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static ContaAgenda MapearConta(SqliteDataReader reader)
    {
        return new ContaAgenda
        {
            ContaAgendaId = reader.GetString(0),
            Provider = reader.GetString(1),
            Estado = reader.GetString(2),
            CursorSync = reader.IsDBNull(3) ? null : reader.GetString(3),
            JanelaSyncInicio = reader.IsDBNull(4) ? null : reader.GetString(4),
            JanelaSyncFim = reader.IsDBNull(5) ? null : reader.GetString(5),
            AtualizadoEm = reader.IsDBNull(6) ? null : reader.GetString(6),
        };
    }

    private static EventoAgenda MapearEvento(SqliteDataReader reader)
    {
        return new EventoAgenda
        {
            EventoAgendaId = reader.GetString(0),
            ContaAgendaId = reader.GetString(1),
            EventoExternoId = reader.GetString(2),
            Titulo = reader.IsDBNull(3) ? null : reader.GetString(3),
            Inicio = reader.GetString(4),
            Fim = reader.GetString(5),
            FusoOriginal = reader.IsDBNull(6) ? null : reader.GetString(6),
            UrlReuniao = reader.IsDBNull(7) ? null : reader.GetString(7),
            Status = reader.IsDBNull(8) ? null : reader.GetString(8),
            AtualizadoEm = reader.IsDBNull(9) ? null : reader.GetString(9),
        };
    }
}
