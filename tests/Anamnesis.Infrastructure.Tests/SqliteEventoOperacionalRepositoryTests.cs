using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Anamnesis.Domain.Entidades;
using Anamnesis.Infrastructure.Observabilidade;
using Anamnesis.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

[Collection(ProcessosExternosGrupo.Nome)]
public sealed class SqliteEventoOperacionalRepositoryTests : IAsyncLifetime
{
    private readonly string _diretorio = Path.Combine(
        Path.GetTempPath(),
        $"anamnesis-journal-{Guid.NewGuid():N}");

    private string CaminhoBanco => Path.Combine(_diretorio, "anamnesis.db");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_diretorio);
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
    public async Task DevePersistirERecarregarEmArquivoSeparadoAposReinicio()
    {
        var reuniaoId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var primeiraInstancia = new SqliteEventoOperacionalRepository(CaminhoBanco);
        await primeiraInstancia.RegistrarAsync(
            CriarEvento(
                new DateTimeOffset(2026, 8, 5, 20, 0, 0, TimeSpan.Zero),
                CodigosEventoOperacional.JobReservado,
                reuniaoId,
                jobId),
            CancellationToken.None);

        var segundaInstancia = new SqliteEventoOperacionalRepository(CaminhoBanco);
        var eventos = await segundaInstancia.ListarAsync(
            new EventoOperacionalFiltro(ReuniaoId: reuniaoId, JobId: jobId),
            CancellationToken.None);

        var evento = Assert.Single(eventos);
        Assert.Equal(CodigosEventoOperacional.JobReservado, evento.Codigo);
        var caminhoJournal = SqliteEventoOperacionalRepository.ResolverCaminhoJournal(CaminhoBanco);
        Assert.Equal(Path.Combine(_diretorio, "anamnesis.journal.db"), caminhoJournal);
        Assert.True(File.Exists(caminhoJournal));
        Assert.False(File.Exists(CaminhoBanco));
    }

    [Fact]
    public async Task DeveFiltrarECalcularMetricasSomenteComDadosPersistidos()
    {
        var reuniaoId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var repository = new SqliteEventoOperacionalRepository(CaminhoBanco);
        await repository.RegistrarAsync(
            CriarEvento(
                new DateTimeOffset(2026, 8, 5, 19, 0, 0, TimeSpan.Zero),
                CodigosEventoOperacional.GravacaoIniciada,
                reuniaoId,
                null,
                NivelEventoOperacional.Info,
                "OBS",
                10),
            CancellationToken.None);
        await repository.RegistrarAsync(
            CriarEvento(
                new DateTimeOffset(2026, 8, 5, 20, 0, 0, TimeSpan.Zero),
                CodigosEventoOperacional.JobReservado,
                reuniaoId,
                jobId,
                NivelEventoOperacional.Aviso,
                "Worker",
                30),
            CancellationToken.None);
        await repository.RegistrarAsync(
            CriarEvento(
                new DateTimeOffset(2026, 8, 5, 21, 0, 0, TimeSpan.Zero),
                CodigosEventoOperacional.OperacaoFalhou,
                Guid.NewGuid(),
                Guid.NewGuid(),
                NivelEventoOperacional.Erro,
                "Whisper",
                null),
            CancellationToken.None);
        var filtro = new EventoOperacionalFiltro(
            NivelEventoOperacional.Aviso,
            "Worker",
            CodigosEventoOperacional.JobReservado,
            reuniaoId,
            jobId,
            new DateTimeOffset(2026, 8, 5, 19, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 5, 20, 30, 0, TimeSpan.Zero));

        var eventos = await repository.ListarAsync(filtro, CancellationToken.None);
        var metricas = await repository.ObterMetricasAsync(
            new EventoOperacionalFiltro(),
            CancellationToken.None);

        Assert.Equal(CodigosEventoOperacional.JobReservado, Assert.Single(eventos).Codigo);
        Assert.Equal(3, metricas.TotalEventos);
        Assert.Equal(2, metricas.Alertas);
        Assert.Equal(20, metricas.DuracaoMediaMs);
    }

    [Fact]
    public async Task DeveRemoverSomenteEventosExpiradosSemTocarBancoPrincipal()
    {
        var reuniao = new Reuniao(
            Guid.NewGuid(),
            "Reunião preservada",
            new DateTimeOffset(2026, 8, 5, 20, 0, 0, TimeSpan.Zero));
        await new SqliteReuniaoRepository(CaminhoBanco).SalvarAsync(reuniao, CancellationToken.None);
        var repository = new SqliteEventoOperacionalRepository(CaminhoBanco);
        await repository.RegistrarAsync(
            CriarEvento(
                new DateTimeOffset(2026, 7, 1, 20, 0, 0, TimeSpan.Zero),
                CodigosEventoOperacional.GravacaoIniciada,
                reuniao.Id,
                null),
            CancellationToken.None);
        await repository.RegistrarAsync(
            CriarEvento(
                new DateTimeOffset(2026, 8, 5, 20, 0, 0, TimeSpan.Zero),
                CodigosEventoOperacional.GravacaoFinalizada,
                reuniao.Id,
                null),
            CancellationToken.None);

        await repository.RemoverAnterioresAsync(
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Equal(CodigosEventoOperacional.GravacaoFinalizada, Assert.Single(
            await repository.ListarAsync(new EventoOperacionalFiltro(), CancellationToken.None)).Codigo);
        Assert.NotNull(await new SqliteReuniaoRepository(CaminhoBanco).ObterAsync(
            reuniao.Id,
            CancellationToken.None));
        await using var conexao = new SqliteConnection($"Data Source={CaminhoBanco};Pooling=False");
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'eventos_operacionais';";
        Assert.Equal(0L, await comando.ExecuteScalarAsync());
    }

    [Fact]
    public async Task DuasInstanciasDevemEscreverConcorrentementeNoMesmoJournal()
    {
        var primeira = new SqliteEventoOperacionalRepository(CaminhoBanco);
        var segunda = new SqliteEventoOperacionalRepository(CaminhoBanco);
        var inicio = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task EscreverLoteAsync(
            SqliteEventoOperacionalRepository repository,
            int indiceInicial)
        {
            await inicio.Task;
            for (var indice = indiceInicial; indice < 40; indice += 2)
            {
                await repository.RegistrarAsync(
                    CriarEvento(
                        new DateTimeOffset(2026, 8, 5, 20, 0, 0, TimeSpan.Zero).AddSeconds(indice),
                        CodigosEventoOperacional.JobReservado,
                        Guid.NewGuid(),
                        Guid.NewGuid()),
                    CancellationToken.None);
            }
        }

        var escritas = new[]
        {
            EscreverLoteAsync(primeira, 0),
            EscreverLoteAsync(segunda, 1)
        };

        inicio.SetResult();
        await Task.WhenAll(escritas);

        var eventos = await new SqliteEventoOperacionalRepository(CaminhoBanco).ListarAsync(
            new EventoOperacionalFiltro(),
            CancellationToken.None);
        Assert.Equal(40, eventos.Count);
    }

    [Fact]
    public async Task DeveReconhecerSchemaCompletoAntesDeRepetirDdl()
    {
        var repository = new SqliteEventoOperacionalRepository(CaminhoBanco);
        await repository.RegistrarAsync(
            CriarEvento(
                new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero),
                CodigosEventoOperacional.JobReservado,
                Guid.NewGuid(),
                Guid.NewGuid()),
            CancellationToken.None);
        var caminhoJournal = SqliteEventoOperacionalRepository.ResolverCaminhoJournal(CaminhoBanco);
        await using var conexao = new SqliteConnection(
            $"Data Source={caminhoJournal};Pooling=False");
        await conexao.OpenAsync();

        var pronto = await SqliteSchema.EventosOperacionaisEstaoInicializadosAsync(
            conexao,
            CancellationToken.None);

        Assert.True(pronto);
    }

    [Fact]
    public async Task DeveEstabilizarPrimeiraInicializacaoConcorrenteEmCemRepeticoes()
    {
        for (var iteracao = 0; iteracao < 100; iteracao++)
        {
            var caminhoBanco = Path.Combine(_diretorio, $"corrida-{iteracao}.db");
            var primeira = new SqliteEventoOperacionalRepository(caminhoBanco);
            var segunda = new SqliteEventoOperacionalRepository(caminhoBanco);
            var inicio = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            async Task RegistrarAsync(
                SqliteEventoOperacionalRepository repository,
                int indice)
            {
                await inicio.Task;
                await repository.RegistrarAsync(
                    CriarEvento(
                        new DateTimeOffset(2026, 8, 6, 13, 0, 0, TimeSpan.Zero)
                            .AddSeconds(indice),
                        CodigosEventoOperacional.JobReservado,
                        Guid.NewGuid(),
                        Guid.NewGuid()),
                    CancellationToken.None);
            }

            var escritas = new[]
            {
                RegistrarAsync(primeira, 0),
                RegistrarAsync(segunda, 1)
            };
            inicio.TrySetResult();
            await Task.WhenAll(escritas);

            var eventos = await new SqliteEventoOperacionalRepository(caminhoBanco).ListarAsync(
                new EventoOperacionalFiltro(),
                CancellationToken.None);
            Assert.Equal(2, eventos.Count);
        }
    }

    [Fact]
    public async Task DeveLimitarConsultaAQuinhentosEventosMaisRecentes()
    {
        var repository = new SqliteEventoOperacionalRepository(CaminhoBanco);
        var inicio = new DateTimeOffset(2026, 8, 5, 20, 0, 0, TimeSpan.Zero);
        for (var indice = 0; indice < 501; indice++)
        {
            await repository.RegistrarAsync(
                CriarEvento(
                    inicio.AddSeconds(indice),
                    CodigosEventoOperacional.JobConcluido,
                    Guid.NewGuid(),
                    Guid.NewGuid()),
                CancellationToken.None);
        }

        var eventos = await repository.ListarAsync(
            new EventoOperacionalFiltro(Limite: 500),
            CancellationToken.None);

        Assert.Equal(500, eventos.Count);
        Assert.Equal(inicio.AddSeconds(500), eventos[0].CriadoEm);
        Assert.DoesNotContain(eventos, evento => evento.CriadoEm == inicio);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.ListarAsync(
            new EventoOperacionalFiltro(Limite: 501),
            CancellationToken.None));
    }

    [Fact]
    public async Task ContencaoNaPrimeiraEscritaNaoDeveBloquearFluxoObservado()
    {
        var caminhoJournal = SqliteEventoOperacionalRepository.ResolverCaminhoJournal(CaminhoBanco);
        await using var bloqueador = new SqliteConnection($"Data Source={caminhoJournal};Pooling=False");
        await bloqueador.OpenAsync();
        await using (var preparar = bloqueador.CreateCommand())
        {
            preparar.CommandText = "CREATE TABLE bloqueio (id INTEGER PRIMARY KEY);";
            await preparar.ExecuteNonQueryAsync();
        }

        await using (var iniciarBloqueio = bloqueador.CreateCommand())
        {
            iniciarBloqueio.CommandText = "BEGIN IMMEDIATE;";
            await iniciarBloqueio.ExecuteNonQueryAsync();
        }
        var journal = new JornalOperacional(
            new SqliteEventoOperacionalRepository(CaminhoBanco),
            TimeProvider.System);
        var cronometro = System.Diagnostics.Stopwatch.StartNew();
        await journal.RegistrarAsync(
            NivelEventoOperacional.Info,
            CodigosEventoOperacional.GravacaoIniciada,
            "OBS",
            "Gravação iniciada.",
            Guid.NewGuid(),
            null,
            null,
            CancellationToken.None);
        cronometro.Stop();
        await using (var liberarBloqueio = bloqueador.CreateCommand())
        {
            liberarBloqueio.CommandText = "ROLLBACK;";
            await liberarBloqueio.ExecuteNonQueryAsync();
        }

        Assert.InRange(cronometro.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ContencaoNaTravaDePreparacaoNaoDeveBloquearFluxoObservado()
    {
        var caminhoJournal = SqliteEventoOperacionalRepository.ResolverCaminhoJournal(CaminhoBanco);
        await using var bloqueador = new FileStream(
            caminhoJournal + ".init.lock",
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 1,
            FileOptions.None);
        var journal = new JornalOperacional(
            new SqliteEventoOperacionalRepository(CaminhoBanco),
            TimeProvider.System);
        var cronometro = System.Diagnostics.Stopwatch.StartNew();

        await journal.RegistrarAsync(
            NivelEventoOperacional.Info,
            CodigosEventoOperacional.GravacaoIniciada,
            "OBS",
            "Gravação iniciada.",
            Guid.NewGuid(),
            null,
            null,
            CancellationToken.None);

        cronometro.Stop();
        Assert.InRange(
            cronometro.Elapsed,
            TimeSpan.FromMilliseconds(750),
            TimeSpan.FromSeconds(2));
    }

    private static EventoOperacional CriarEvento(
        DateTimeOffset criadoEm,
        string codigo,
        Guid reuniaoId,
        Guid? jobId,
        NivelEventoOperacional nivel = NivelEventoOperacional.Info,
        string componente = "Worker",
        double? duracaoMs = null) =>
        new(
            Guid.NewGuid(),
            criadoEm,
            nivel,
            codigo,
            componente,
            "Mensagem segura.",
            reuniaoId,
            jobId,
            new MetadadosEventoOperacional(
                Operacao: "teste",
                Tentativa: 1,
                Resultado: "sucesso",
                MotivoCodigo: null,
                DuracaoMs: duracaoMs));
}
