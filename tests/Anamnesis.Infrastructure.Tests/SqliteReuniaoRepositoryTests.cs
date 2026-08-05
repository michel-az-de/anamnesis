using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.UseCases;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Anamnesis.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class SqliteReuniaoRepositoryTests : IAsyncLifetime
{
    private readonly string _caminhoBanco = Path.Combine(Path.GetTempPath(), $"anamnesis-{Guid.NewGuid():N}.db");

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        if (File.Exists(_caminhoBanco))
        {
            File.Delete(_caminhoBanco);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task DeveRecuperarGravacaoEEstadoDaReuniao()
    {
        var reuniao = CriarReuniaoAguardandoProcessamento();
        var repository = new SqliteReuniaoRepository(_caminhoBanco);

        await repository.SalvarAsync(reuniao, CancellationToken.None);
        var recuperada = await repository.ObterAsync(reuniao.Id, CancellationToken.None);

        Assert.NotNull(recuperada);
        Assert.Equal(reuniao.Id, recuperada!.Id);
        Assert.Equal("Planejamento", recuperada.Titulo);
        Assert.Equal(StatusReuniao.AguardandoProcessamento, recuperada.Status);
        Assert.Equal(@"C:\gravacoes\planejamento.mkv", recuperada.Gravacao!.CaminhoArquivo);
        var gravacaoOriginal = Assert.IsType<Gravacao>(reuniao.Gravacao);
        Assert.Equal(gravacaoOriginal.IniciadaEm, recuperada.Gravacao.IniciadaEm);
        Assert.Equal(gravacaoOriginal.FinalizadaEm, recuperada.Gravacao.FinalizadaEm);
    }

    [Fact]
    public async Task DeveRecuperarArtefatosDeUmaReuniaoArquivada()
    {
        var reuniao = CriarReuniaoArquivada();
        var repository = new SqliteReuniaoRepository(_caminhoBanco);

        await repository.SalvarAsync(reuniao, CancellationToken.None);
        var recuperada = await repository.ObterAsync(reuniao.Id, CancellationToken.None);

        Assert.NotNull(recuperada);
        Assert.Equal(StatusReuniao.Arquivada, recuperada!.Status);
        Assert.Equal(reuniao.ArquivadaEm, recuperada.ArquivadaEm);
        Assert.Equal("Transcrição da reunião.", recuperada.Transcricao!.Texto);
        Assert.Equal("pt-BR", recuperada.Transcricao.Idioma);
        Assert.Equal("Resumo executivo.", recuperada.Ata!.ResumoExecutivo);
        Assert.Equal(["Aprovar o plano."], recuperada.Ata.Decisoes);
        var tarefa = Assert.Single(recuperada.Ata.Tarefas);
        Assert.Equal("Preparar proposta.", tarefa.Descricao);
        Assert.Equal("Ana", tarefa.Responsavel);
        Assert.Equal(new DateOnly(2026, 8, 8), tarefa.Prazo);
    }

    [Fact]
    public async Task DeveAtualizarUmaReuniaoJaPersistida()
    {
        var reuniao = CriarReuniaoAguardandoProcessamento();
        var repository = new SqliteReuniaoRepository(_caminhoBanco);
        await repository.SalvarAsync(reuniao, CancellationToken.None);

        reuniao.IniciarTranscricao();
        await repository.SalvarAsync(reuniao, CancellationToken.None);
        var recuperada = await repository.ObterAsync(reuniao.Id, CancellationToken.None);

        Assert.NotNull(recuperada);
        Assert.Equal(StatusReuniao.EmTranscricao, recuperada!.Status);
    }

    [Fact]
    public async Task DeveRetornarNuloParaReuniaoInexistente()
    {
        var repository = new SqliteReuniaoRepository(_caminhoBanco);

        var recuperada = await repository.ObterAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(recuperada);
    }

    [Fact]
    public async Task DeveRecuperarMotivoDeFalha()
    {
        var reuniao = CriarReuniaoAguardandoProcessamento();
        reuniao.RegistrarFalha("Whisper indisponível.");
        var repository = new SqliteReuniaoRepository(_caminhoBanco);

        await repository.SalvarAsync(reuniao, CancellationToken.None);
        var recuperada = await repository.ObterAsync(reuniao.Id, CancellationToken.None);

        Assert.NotNull(recuperada);
        Assert.Equal(StatusReuniao.Falha, recuperada!.Status);
        Assert.Equal("Whisper indisponível.", recuperada.MotivoFalha);
    }

    [Fact]
    public async Task DevePermitirUmaUnicaGravacaoEmInicializacoesConcorrentes()
    {
        var repository = new SqliteReuniaoRepository(_caminhoBanco);
        var gravador = new GravadorContador();
        var preflight = new ObsPreflightContador();
        var sinal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handlers = Enumerable.Range(0, 2)
            .Select(_ => new ControlarGravacaoHandler(
                repository,
                new JobQueueNula(),
                gravador,
                new WorkerLauncherNulo(),
                preflight,
                TimeProvider.System))
            .ToArray();

        async Task<(Guid? ReuniaoId, Exception? Falha)> TentarAsync(ControlarGravacaoHandler handler)
        {
            await sinal.Task;
            try
            {
                return (await handler.IniciarAsync("Concorrente", CancellationToken.None), null);
            }
            catch (Exception exception)
            {
                return (null, exception);
            }
        }

        var tentativas = handlers.Select(TentarAsync).ToArray();
        sinal.SetResult();
        var resultados = await Task.WhenAll(tentativas);

        Assert.Single(resultados, resultado => resultado.ReuniaoId.HasValue);
        Assert.Single(resultados, resultado => resultado.Falha is GravacaoJaAtivaException);
        Assert.Equal(1, gravador.Inicios);
        Assert.Equal(1, preflight.Preparacoes);
    }

    [Fact]
    public async Task DevePermitirNovaGravacaoAposFalhaAoEncerrar()
    {
        var repository = new SqliteReuniaoRepository(_caminhoBanco);
        var handler = new ControlarGravacaoHandler(
            repository,
            new JobQueueNula(),
            new GravadorContador(),
            new WorkerLauncherNulo(),
            new ObsPreflightContador(),
            TimeProvider.System);
        var primeiraId = await handler.IniciarAsync("Primeira", CancellationToken.None);

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            handler.FinalizarAsync(primeiraId, CancellationToken.None));

        // O índice único de gravação ativa só libera se a reunião sair de 'Gravando'.
        // Sem isso o app fica sem gravar até o Tray ser reiniciado.
        var segundaId = await handler.IniciarAsync("Segunda", CancellationToken.None);

        Assert.NotEqual(primeiraId, segundaId);
        var primeira = await repository.ObterAsync(primeiraId, CancellationToken.None);
        Assert.Equal(StatusReuniao.Falha, primeira!.Status);
    }

    [Fact]
    public async Task DevePermitirNovaGravacaoAposCancelamentoAoEncerrar()
    {
        using var cancelamento = new CancellationTokenSource();
        var repository = new SqliteReuniaoRepository(_caminhoBanco);
        var gravador = new GravadorQueCancelaStop(cancelamento);
        var handler = new ControlarGravacaoHandler(
            repository,
            new JobQueueNula(),
            gravador,
            new WorkerLauncherNulo(),
            new ObsPreflightContador(),
            TimeProvider.System);
        var primeiraId = await handler.IniciarAsync("Primeira", CancellationToken.None);

        var excecao = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.FinalizarAsync(primeiraId, cancelamento.Token));

        Assert.Same(gravador.Excecao, excecao);
        var primeira = await repository.ObterAsync(primeiraId, CancellationToken.None);
        Assert.Equal(StatusReuniao.Falha, primeira!.Status);

        var segundaId = await handler.IniciarAsync("Segunda", CancellationToken.None);
        Assert.NotEqual(primeiraId, segundaId);
    }

    [Fact]
    public async Task DeveReconciliarDuplicatasGravandoAntesDeCriarIndiceEmBancoLegado()
    {
        var repository = new SqliteReuniaoRepository(_caminhoBanco);
        await repository.ObterAsync(Guid.NewGuid(), CancellationToken.None);
        var antigaId = Guid.NewGuid();
        var recenteId = Guid.NewGuid();
        await using (var conexao = new SqliteConnection($"Data Source={_caminhoBanco};Pooling=False"))
        {
            await conexao.OpenAsync();
            await using var comando = conexao.CreateCommand();
            comando.CommandText = """
                DROP INDEX ux_reunioes_gravando;
                INSERT INTO reunioes (id, titulo, criada_em, status)
                VALUES ($antiga_id, 'Antiga', '2026-08-05T10:00:00.0000000+00:00', 'Gravando');
                INSERT INTO reunioes (id, titulo, criada_em, status)
                VALUES ($recente_id, 'Recente', '2026-08-05T11:00:00.0000000+00:00', 'Gravando');
                """;
            comando.Parameters.AddWithValue("$antiga_id", antigaId.ToString("N"));
            comando.Parameters.AddWithValue("$recente_id", recenteId.ToString("N"));
            await comando.ExecuteNonQueryAsync();
        }

        var reunioes = await new SqliteReuniaoQuery(_caminhoBanco).ListarAsync(
            new ReuniaoQueryFiltro(null, null, 100),
            CancellationToken.None);

        Assert.Equal(recenteId, Assert.Single(reunioes, item => item.Status == StatusReuniao.Gravando).Id);
        var antiga = Assert.Single(reunioes, item => item.Id == antigaId);
        Assert.Equal(StatusReuniao.Falha, antiga.Status);
        Assert.Contains("legado", antiga.MotivoFalha, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DevePrepararEsquemaUmaUnicaVezPorInstancia()
    {
        var repository = new SqliteReuniaoRepository(_caminhoBanco);

        await repository.ObterAsync(Guid.NewGuid(), CancellationToken.None);
        await repository.SalvarAsync(CriarReuniaoArquivada(), CancellationToken.None);
        await repository.ObterAsync(Guid.NewGuid(), CancellationToken.None);

        // Antes, cada operação repetia DDL, consulta a pragma_table_info, um UPDATE sobre a
        // tabela inteira e um CREATE INDEX — e ainda abria uma segunda conexão só para isso.
        Assert.Equal(1, repository.PreparacoesDeEsquema);
    }

    [Fact]
    public async Task DeveManterOBancoLocalEmModoWal()
    {
        var repository = new SqliteReuniaoRepository(_caminhoBanco);
        await repository.ObterAsync(Guid.NewGuid(), CancellationToken.None);

        await using var conexao = new SqliteConnection($"Data Source={_caminhoBanco};Pooling=False");
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "PRAGMA journal_mode;";

        Assert.Equal("wal", (string)(await comando.ExecuteScalarAsync())!, ignoreCase: true);
    }

    private static Reuniao CriarReuniaoAguardandoProcessamento()
    {
        var criadaEm = new DateTimeOffset(2026, 8, 4, 14, 0, 0, TimeSpan.Zero);
        var reuniao = new Reuniao(Guid.NewGuid(), "Planejamento", criadaEm);
        reuniao.IniciarGravacao(criadaEm.AddMinutes(5));
        reuniao.FinalizarGravacao(@"C:\gravacoes\planejamento.mkv", criadaEm.AddHours(1));
        return reuniao;
    }

    private static Reuniao CriarReuniaoArquivada()
    {
        var reuniao = CriarReuniaoAguardandoProcessamento();
        reuniao.IniciarTranscricao();
        reuniao.RegistrarTranscricao(new Transcricao("Transcrição da reunião.", "pt-BR", new DateTimeOffset(2026, 8, 4, 15, 10, 0, TimeSpan.Zero)));
        reuniao.RegistrarAta(new Ata(
            "Resumo executivo.",
            ["Aprovar o plano."],
            [new Tarefa("Preparar proposta.", "Ana", new DateOnly(2026, 8, 8))],
            new DateTimeOffset(2026, 8, 4, 15, 15, 0, TimeSpan.Zero)));
        reuniao.MarcarArquivada(new DateTimeOffset(2026, 8, 4, 15, 20, 0, TimeSpan.Zero));
        return reuniao;
    }

    private sealed class GravadorContador : IGravador
    {
        private int _inicios;

        public int Inicios => _inicios;

        public Task IniciarAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _inicios);
            return Task.CompletedTask;
        }

        public Task<bool> EstaGravandoAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<string> FinalizarAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class GravadorQueCancelaStop(CancellationTokenSource cancelamento) : IGravador
    {
        public OperationCanceledException Excecao { get; } =
            new("StopRecord cancelado.", cancelamento.Token);

        public Task IniciarAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> EstaGravandoAsync(CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<string> FinalizarAsync(CancellationToken cancellationToken)
        {
            cancelamento.Cancel();
            return Task.FromException<string>(Excecao);
        }
    }

    private sealed class ObsPreflightContador : IObsPreflight
    {
        private int _preparacoes;

        public int Preparacoes => _preparacoes;

        public Task PrepararAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _preparacoes);
            return Task.CompletedTask;
        }
    }

    private sealed class JobQueueNula : IJobQueue
    {
        public Task<JobProcessamento> EnfileirarAsync(Guid reuniaoId, DateTimeOffset criadoEm, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<JobProcessamento?> ReservarProximoAsync(DateTimeOffset reservadoEm, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task LiberarAsync(Guid jobId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task LiberarReservasAtivasAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ConcluirAsync(Guid jobId, DateTimeOffset concluidoEm, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class WorkerLauncherNulo : IWorkerLauncher
    {
        public Task IniciarAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
