using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Anamnesis.Domain.Entidades;
using Anamnesis.Infrastructure.Persistencia;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class SqliteAgendaCacheTests : IAsyncLifetime
{
    private readonly string _diretorio = Path.Combine(
        Path.GetTempPath(),
        $"anamnesis-agenda-cache-{Guid.NewGuid():N}");
    private string _caminhoDb = string.Empty;
    private SqliteAgendaCache _cache = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_diretorio);
        _caminhoDb = Path.Combine(_diretorio, "agenda-cache.db");
        _cache = new SqliteAgendaCache(_caminhoDb, pooling: false);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_diretorio))
        {
            Directory.Delete(_diretorio, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task SalvarConta_RetornaContaSalva()
    {
        var conta = new ContaAgenda
        {
            ContaAgendaId = "conta-01",
            Provider = "Google",
            Estado = "Conectada",
            CursorSync = "token-abc",
            JanelaSyncInicio = "2026-08-01T00:00:00Z",
            JanelaSyncFim = "2026-08-31T23:59:59Z",
            AtualizadoEm = DateTime.UtcNow.ToString("O"),
        };

        await _cache.SalvarContaAsync(conta);
        var recuperada = await _cache.ObterContaAsync("conta-01");

        Assert.NotNull(recuperada);
        Assert.Equal("Google", recuperada.Provider);
        Assert.Equal("Conectada", recuperada.Estado);
        Assert.Equal("token-abc", recuperada.CursorSync);
    }

    [Fact]
    public async Task AtualizarConta_SobrescreveDados()
    {
        var conta = new ContaAgenda
        {
            ContaAgendaId = "conta-02",
            Provider = "Microsoft",
            Estado = "Conectada",
        };
        await _cache.SalvarContaAsync(conta);

        conta.Estado = "Sincronizando";
        conta.CursorSync = "novo-token";
        await _cache.SalvarContaAsync(conta);

        var recuperada = await _cache.ObterContaAsync("conta-02");
        Assert.Equal("Sincronizando", recuperada!.Estado);
        Assert.Equal("novo-token", recuperada.CursorSync);
    }

    [Fact]
    public async Task ListarContas_RetornaTodas()
    {
        await _cache.SalvarContaAsync(new ContaAgenda { ContaAgendaId = "c1", Provider = "Google", Estado = "Conectada" });
        await _cache.SalvarContaAsync(new ContaAgenda { ContaAgendaId = "c2", Provider = "Microsoft", Estado = "Conectada" });

        var contas = await _cache.ListarContasAsync();

        Assert.Equal(2, contas.Count);
    }

    [Fact]
    public async Task RemoverConta_ExcluiContaEEeventos()
    {
        await _cache.SalvarContaAsync(new ContaAgenda { ContaAgendaId = "c3", Provider = "Google", Estado = "Conectada" });
        await _cache.SalvarEventosAsync("c3", new[]
        {
            new EventoAgenda { EventoAgendaId = "e1", EventoExternoId = "ext1", Inicio = "2026-08-07T10:00:00Z", Fim = "2026-08-07T11:00:00Z" },
        });

        await _cache.RemoverContaAsync("c3");

        var conta = await _cache.ObterContaAsync("c3");
        var eventos = await _cache.ListarEventosAsync("c3");

        Assert.Null(conta);
        Assert.Empty(eventos);
    }

    [Fact]
    public async Task SalvarEventos_SubstituiEventosExistentes()
    {
        await _cache.SalvarContaAsync(new ContaAgenda { ContaAgendaId = "c4", Provider = "Google", Estado = "Conectada" });
        await _cache.SalvarEventosAsync("c4", new[]
        {
            new EventoAgenda { EventoAgendaId = "e1", EventoExternoId = "ext1", Titulo = "Old", Inicio = "2026-08-07T10:00:00Z", Fim = "2026-08-07T11:00:00Z" },
        });

        await _cache.SalvarEventosAsync("c4", new[]
        {
            new EventoAgenda { EventoAgendaId = "e2", EventoExternoId = "ext2", Titulo = "New", Inicio = "2026-08-07T12:00:00Z", Fim = "2026-08-07T13:00:00Z" },
        });

        var eventos = await _cache.ListarEventosAsync("c4");

        Assert.Single(eventos);
        Assert.Equal("New", eventos[0].Titulo);
    }

    [Fact]
    public async Task ListarEventosProximos_RetornaApenasEventosDentroDoIntervalo()
    {
        await _cache.SalvarContaAsync(new ContaAgenda { ContaAgendaId = "c5", Provider = "Google", Estado = "Conectada" });
        var agora = DateTime.UtcNow;
        await _cache.SalvarEventosAsync("c5", new[]
        {
            new EventoAgenda { EventoAgendaId = "e1", EventoExternoId = "ext1", Titulo = "Proximo", Inicio = agora.AddMinutes(10).ToString("O"), Fim = agora.AddMinutes(40).ToString("O") },
            new EventoAgenda { EventoAgendaId = "e2", EventoExternoId = "ext2", Titulo = "Distante", Inicio = agora.AddHours(5).ToString("O"), Fim = agora.AddHours(6).ToString("O") },
        });

        var proximos = await _cache.ListarEventosProximosAsync(30);

        Assert.Single(proximos);
        Assert.Equal("Proximo", proximos[0].Titulo);
    }

    [Fact]
    public async Task ListarEventosProximos_IgnoraCancelados()
    {
        await _cache.SalvarContaAsync(new ContaAgenda { ContaAgendaId = "c6", Provider = "Google", Estado = "Conectada" });
        var agora = DateTime.UtcNow;
        await _cache.SalvarEventosAsync("c6", new[]
        {
            new EventoAgenda { EventoAgendaId = "e1", EventoExternoId = "ext1", Titulo = "Cancelado", Inicio = agora.AddMinutes(10).ToString("O"), Fim = agora.AddMinutes(40).ToString("O"), Status = "Cancelado" },
        });

        var proximos = await _cache.ListarEventosProximosAsync(30);

        Assert.Empty(proximos);
    }

    [Fact]
    public async Task RemoverEventosForaDaJanela_ExcluiApenarForaDoIntervalo()
    {
        await _cache.SalvarContaAsync(new ContaAgenda { ContaAgendaId = "c7", Provider = "Google", Estado = "Conectada" });
        await _cache.SalvarEventosAsync("c7", new[]
        {
            new EventoAgenda { EventoAgendaId = "e1", EventoExternoId = "ext1", Titulo = "Antigo", Inicio = "2026-07-01T10:00:00Z", Fim = "2026-07-01T11:00:00Z" },
            new EventoAgenda { EventoAgendaId = "e2", EventoExternoId = "ext2", Titulo = "Dentro", Inicio = "2026-08-07T10:00:00Z", Fim = "2026-08-07T11:00:00Z" },
        });

        await _cache.RemoverEventosForaDaJanelaAsync("c7", "2026-08-01T00:00:00Z", "2026-08-31T23:59:59Z");

        var eventos = await _cache.ListarEventosAsync("c7");

        Assert.Single(eventos);
        Assert.Equal("Dentro", eventos[0].Titulo);
    }

    [Fact]
    public async Task EventoComUrlReuniao_PersisteUrl()
    {
        await _cache.SalvarContaAsync(new ContaAgenda { ContaAgendaId = "c8", Provider = "Google", Estado = "Conectada" });
        await _cache.SalvarEventosAsync("c8", new[]
        {
            new EventoAgenda { EventoAgendaId = "e1", EventoExternoId = "ext1", Titulo = "Meet", Inicio = "2026-08-07T10:00:00Z", Fim = "2026-08-07T11:00:00Z", UrlReuniao = "https://meet.google.com/abc-defg-hij" },
        });

        var eventos = await _cache.ListarEventosAsync("c8");

        Assert.Equal("https://meet.google.com/abc-defg-hij", eventos[0].UrlReuniao);
    }
}
