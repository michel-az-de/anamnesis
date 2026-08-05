using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Anamnesis.Application.UseCases;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Xunit;

namespace Anamnesis.Application.Tests;

public sealed class ControlarGravacaoHandlerTests
{
    [Fact]
    public async Task DevePersistirReuniaoEIniciarGravacao()
    {
        var repository = new ReuniaoRepositoryFake();
        var eventos = new List<string>();
        var gravador = new GravadorFake(eventos: eventos);
        var handler = CriarHandler(
            repository,
            new JobQueueFake(),
            gravador,
            obsPreflight: new ObsPreflightFake(eventos));

        var reuniaoId = await handler.IniciarAsync("Teste", CancellationToken.None);

        var reuniao = Assert.Single(repository.Salvas);
        Assert.Equal(reuniaoId, reuniao.Id);
        Assert.Equal(StatusReuniao.Gravando, reuniao.Status);
        Assert.True(gravador.Iniciou);
        Assert.Equal(["obs-preflight", "gravador"], eventos);
    }

    [Fact]
    public async Task DeveFinalizarGravacaoEPersistirJob()
    {
        var repository = new ReuniaoRepositoryFake();
        var eventos = new List<string>();
        var fila = new JobQueueFake(eventos);
        var worker = new WorkerLauncherFake(eventos);
        var handler = CriarHandler(repository, fila, new GravadorFake(@"C:\gravacoes\teste.mkv"), worker);
        var reuniaoId = await handler.IniciarAsync("Teste", CancellationToken.None);

        await handler.FinalizarAsync(reuniaoId, CancellationToken.None);

        var reuniao = Assert.Single(repository.Salvas);
        Assert.Equal(StatusReuniao.AguardandoProcessamento, reuniao.Status);
        Assert.Equal(@"C:\gravacoes\teste.mkv", reuniao.Gravacao!.CaminhoArquivo);
        Assert.Equal(reuniaoId, fila.ReuniaoEnfileiradaId);
        Assert.True(worker.Iniciou);
        Assert.Equal(["job", "worker"], eventos);
    }

    [Fact]
    public async Task DeveRegistrarMarcosReaisComCorrelacaoDoJob()
    {
        var repository = new ReuniaoRepositoryFake();
        var fila = new JobQueueFake();
        var sink = new EventoSinkFake();
        var handler = CriarHandler(
            repository,
            fila,
            new GravadorFake(@"C:\gravacoes\teste.mkv"),
            journal: new JornalOperacional(sink, TimeProvider.System));

        var reuniaoId = await handler.IniciarAsync("Teste", CancellationToken.None);
        await handler.FinalizarAsync(reuniaoId, CancellationToken.None);

        Assert.Equal(
            [
                CodigosEventoOperacional.GravacaoIniciada,
                CodigosEventoOperacional.GravacaoFinalizada,
                CodigosEventoOperacional.JobEnfileirado
            ],
            sink.Eventos.Select(evento => evento.Codigo));
        Assert.All(sink.Eventos, evento => Assert.Equal(reuniaoId, evento.ReuniaoId));
        Assert.Null(sink.Eventos[0].JobId);
        Assert.Null(sink.Eventos[1].JobId);
        Assert.Equal(fila.JobCriadoId, sink.Eventos[2].JobId);
    }

    [Fact]
    public async Task DevePreservarJobQuandoWorkerNaoInicia()
    {
        var repository = new ReuniaoRepositoryFake();
        var fila = new JobQueueFake();
        var worker = new WorkerLauncherFake(falhar: true);
        var handler = CriarHandler(repository, fila, new GravadorFake(@"C:\gravacoes\segura.mkv"), worker);
        var reuniaoId = await handler.IniciarAsync("Teste", CancellationToken.None);

        var excecao = await Assert.ThrowsAsync<WorkerNaoIniciadoException>(() =>
            handler.FinalizarAsync(reuniaoId, CancellationToken.None));

        Assert.Equal("A gravação foi salva, mas o Worker não iniciou: Worker indisponível.", excecao.Message);
        Assert.Equal("Worker indisponível.", excecao.InnerException!.Message);
        Assert.Equal(reuniaoId, fila.ReuniaoEnfileiradaId);
        Assert.Equal(StatusReuniao.AguardandoProcessamento, Assert.Single(repository.Salvas).Status);
    }

    [Fact]
    public async Task FalhaAoEnfileirarDeveGerarEventoSeguroDaFila()
    {
        var repository = new ReuniaoRepositoryFake();
        var fila = new JobQueueFake(falharAoEnfileirar: true);
        var sink = new EventoSinkFake();
        var handler = CriarHandler(
            repository,
            fila,
            new GravadorFake(@"C:\gravacoes\segura.mkv"),
            journal: new JornalOperacional(sink, TimeProvider.System));
        var reuniaoId = await handler.IniciarAsync("Fila indisponível", CancellationToken.None);

        await Assert.ThrowsAsync<IOException>(() =>
            handler.FinalizarAsync(reuniaoId, CancellationToken.None));

        var falha = sink.Eventos.Last();
        Assert.Equal(CodigosEventoOperacional.OperacaoFalhou, falha.Codigo);
        Assert.Equal("Fila", falha.Componente);
        Assert.Equal("enfileirar_job", falha.Metadados.Operacao);
        Assert.Equal(reuniaoId, falha.ReuniaoId);
        Assert.Null(falha.JobId);
    }

    [Fact]
    public async Task DeveRegistrarFalhaQuandoGravadorNaoEncerra()
    {
        var repository = new ReuniaoRepositoryFake();
        var fila = new JobQueueFake();
        var handler = CriarHandler(repository, fila, new GravadorQueNaoEncerra());
        var reuniaoId = await handler.IniciarAsync("Teste", CancellationToken.None);

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.FinalizarAsync(reuniaoId, CancellationToken.None));

        Assert.Equal("OBS não respondeu ao encerrar.", excecao.Message);
        var reuniao = Assert.Single(repository.Salvas);
        Assert.Equal(StatusReuniao.Falha, reuniao.Status);
        Assert.Equal("OBS não respondeu ao encerrar.", reuniao.MotivoFalha);
        Assert.Null(fila.ReuniaoEnfileiradaId);
    }

    [Fact]
    public async Task DeveCompensarCancelamentoAoEncerrarEPreservarExcecaoOriginal()
    {
        using var cancelamento = new CancellationTokenSource();
        var repository = new ReuniaoRepositoryFake();
        var fila = new JobQueueFake();
        var gravador = new GravadorQueCancelaAoEncerrar(cancelamento);
        var handler = CriarHandler(repository, fila, gravador);
        var reuniaoId = await handler.IniciarAsync("Teste", CancellationToken.None);

        var excecao = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.FinalizarAsync(reuniaoId, cancelamento.Token));

        Assert.Same(gravador.Excecao, excecao);
        var reuniao = Assert.Single(repository.Salvas);
        Assert.Equal(StatusReuniao.Falha, reuniao.Status);
        Assert.Equal("StopRecord cancelado.", reuniao.MotivoFalha);
        Assert.Null(fila.ReuniaoEnfileiradaId);
        Assert.Equal(2, repository.TokensDeSalvamento.Count);
        Assert.Equal(CancellationToken.None, repository.TokensDeSalvamento[^1]);
    }

    [Fact]
    public async Task DeveRegistrarFalhaSemEnfileirarJob()
    {
        var repository = new ReuniaoRepositoryFake();
        var fila = new JobQueueFake();
        var sink = new EventoSinkFake();
        var handler = CriarHandler(
            repository,
            fila,
            new GravadorComFalha(),
            journal: new JornalOperacional(sink, TimeProvider.System));

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.IniciarAsync("Teste", CancellationToken.None));

        var reuniao = Assert.Single(repository.Salvas);
        Assert.Equal("OBS indisponível.", excecao.Message);
        Assert.Equal(StatusReuniao.Falha, reuniao.Status);
        Assert.Null(fila.ReuniaoEnfileiradaId);
        var evento = Assert.Single(sink.Eventos);
        Assert.Equal(CodigosEventoOperacional.OperacaoFalhou, evento.Codigo);
        Assert.Equal(reuniao.Id, evento.ReuniaoId);
        Assert.Null(evento.JobId);
    }

    [Fact]
    public async Task DeveRegistrarFalhaDoPreflightSemChamarGravador()
    {
        var repository = new ReuniaoRepositoryFake();
        var fila = new JobQueueFake();
        var gravador = new GravadorFake();
        var handler = CriarHandler(
            repository,
            fila,
            gravador,
            obsPreflight: new ObsPreflightFake(falhar: true));

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.IniciarAsync("Teste", CancellationToken.None));

        Assert.Equal("OBS não ficou disponível.", excecao.Message);
        Assert.False(gravador.Iniciou);
        Assert.Equal(StatusReuniao.Falha, Assert.Single(repository.Salvas).Status);
        Assert.Null(fila.ReuniaoEnfileiradaId);
    }

    [Fact]
    public async Task DeveRecusarGravacaoAtivaAntesDePrepararObs()
    {
        var eventos = new List<string>();
        var gravador = new GravadorFake(eventos: eventos);
        var handler = CriarHandler(
            new ReuniaoRepositoryComGravacaoAtiva(),
            new JobQueueFake(),
            gravador,
            obsPreflight: new ObsPreflightFake(eventos));

        var excecao = await Assert.ThrowsAsync<GravacaoJaAtivaException>(() =>
            handler.IniciarAsync("Concorrente", CancellationToken.None));

        Assert.Equal("Já existe uma reunião sendo gravada.", excecao.Message);
        Assert.Empty(eventos);
        Assert.False(gravador.Iniciou);
    }

    [Fact]
    public async Task DeveCompensarReservaQuandoInicioForCancelado()
    {
        var repository = new ReuniaoRepositoryFake();
        var handler = CriarHandler(
            repository,
            new JobQueueFake(),
            new GravadorFake(),
            obsPreflight: new ObsPreflightCancelado());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.IniciarAsync("Cancelada", CancellationToken.None));

        Assert.Equal(StatusReuniao.Falha, Assert.Single(repository.Salvas).Status);
    }

    [Fact]
    public async Task DeveReconciliarReservaOrfaQuandoObsNaoEstaGravando()
    {
        var repository = new ReuniaoRepositoryFake();
        var reuniao = new Reuniao(Guid.NewGuid(), "Órfã", DateTimeOffset.UtcNow);
        reuniao.IniciarGravacao(DateTimeOffset.UtcNow);
        await repository.SalvarAsync(reuniao, CancellationToken.None);
        var handler = CriarHandler(
            repository,
            new JobQueueFake(),
            new GravadorFake(estaGravando: false));

        await handler.ReconciliarGravacaoAsync(reuniao.Id, CancellationToken.None);

        Assert.Equal(StatusReuniao.Falha, reuniao.Status);
        Assert.Contains("interrompida", reuniao.MotivoFalha, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FalhaAoConsultarObsNaReconciliacaoDeveManterCorrelacaoDaReuniao()
    {
        var repository = new ReuniaoRepositoryFake();
        var reuniao = new Reuniao(Guid.NewGuid(), "Órfã", DateTimeOffset.UtcNow);
        reuniao.IniciarGravacao(DateTimeOffset.UtcNow);
        await repository.SalvarAsync(reuniao, CancellationToken.None);
        var sink = new EventoSinkFake();
        var handler = CriarHandler(
            repository,
            new JobQueueFake(),
            new GravadorFake(),
            obsPreflight: new ObsPreflightFake(falhar: true),
            journal: new JornalOperacional(sink, TimeProvider.System));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ReconciliarGravacaoAsync(reuniao.Id, CancellationToken.None));

        var evento = Assert.Single(sink.Eventos);
        Assert.Equal(CodigosEventoOperacional.OperacaoFalhou, evento.Codigo);
        Assert.Equal("OBS", evento.Componente);
        Assert.Equal("reconciliar_gravacao", evento.Metadados.Operacao);
        Assert.Equal(reuniao.Id, evento.ReuniaoId);
        Assert.Equal(StatusReuniao.Gravando, reuniao.Status);
    }

    private static ControlarGravacaoHandler CriarHandler(
        IReuniaoRepository repository,
        JobQueueFake fila,
        IGravador gravador,
        IWorkerLauncher? workerLauncher = null,
        IObsPreflight? obsPreflight = null,
        JornalOperacional? journal = null) =>
        new(
            repository,
            fila,
            gravador,
            workerLauncher ?? new WorkerLauncherFake(),
            obsPreflight ?? new ObsPreflightFake(),
            TimeProvider.System,
            journal);

    private sealed class ReuniaoRepositoryComGravacaoAtiva : IReuniaoRepository
    {
        public Task<Reuniao?> ObterAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult<Reuniao?>(null);

        public Task SalvarAsync(Reuniao reuniao, CancellationToken cancellationToken) =>
            Task.FromException(new GravacaoJaAtivaException());
    }

    private sealed class ReuniaoRepositoryFake : IReuniaoRepository
    {
        private readonly Dictionary<Guid, Reuniao> _reunioes = [];
        public List<Reuniao> Salvas { get; } = [];
        public List<CancellationToken> TokensDeSalvamento { get; } = [];

        public Task<Reuniao?> ObterAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult(_reunioes.GetValueOrDefault(reuniaoId));

        public Task SalvarAsync(Reuniao reuniao, CancellationToken cancellationToken)
        {
            TokensDeSalvamento.Add(cancellationToken);
            _reunioes[reuniao.Id] = reuniao;
            if (!Salvas.Contains(reuniao))
            {
                Salvas.Add(reuniao);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class JobQueueFake(
        List<string>? eventos = null,
        bool falharAoEnfileirar = false) : IJobQueue
    {
        public Guid? ReuniaoEnfileiradaId { get; private set; }
        public Guid? JobCriadoId { get; private set; }

        public Task<JobProcessamento> EnfileirarAsync(Guid reuniaoId, DateTimeOffset criadoEm, CancellationToken cancellationToken)
        {
            if (falharAoEnfileirar)
            {
                throw new IOException("Fila local indisponível.");
            }

            ReuniaoEnfileiradaId = reuniaoId;
            JobCriadoId = Guid.NewGuid();
            eventos?.Add("job");
            return Task.FromResult(new JobProcessamento(JobCriadoId.Value, reuniaoId, criadoEm, null, 0));
        }

        public Task<JobProcessamento?> ReservarProximoAsync(DateTimeOffset reservadoEm, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task LiberarAsync(Guid jobId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task LiberarReservasAtivasAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ConcluirAsync(Guid jobId, DateTimeOffset concluidoEm, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class GravadorFake(
        string caminhoArquivo = "",
        List<string>? eventos = null,
        bool estaGravando = true) : IGravador
    {
        public bool Iniciou { get; private set; }
        public Task IniciarAsync(CancellationToken cancellationToken)
        {
            Iniciou = true;
            eventos?.Add("gravador");
            return Task.CompletedTask;
        }

        public Task<string> FinalizarAsync(CancellationToken cancellationToken) => Task.FromResult(caminhoArquivo);

        public Task<bool> EstaGravandoAsync(CancellationToken cancellationToken) =>
            Task.FromResult(estaGravando);
    }

    private sealed class ObsPreflightFake(List<string>? eventos = null, bool falhar = false) : IObsPreflight
    {
        public Task PrepararAsync(CancellationToken cancellationToken)
        {
            eventos?.Add("obs-preflight");
            return falhar
                ? Task.FromException(new InvalidOperationException("OBS não ficou disponível."))
                : Task.CompletedTask;
        }
    }

    private sealed class ObsPreflightCancelado : IObsPreflight
    {
        public Task PrepararAsync(CancellationToken cancellationToken) =>
            Task.FromCanceled(new CancellationToken(canceled: true));
    }

    private sealed class GravadorQueNaoEncerra : IGravador
    {
        public Task IniciarAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string> FinalizarAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("OBS não respondeu ao encerrar.");

        public Task<bool> EstaGravandoAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class GravadorQueCancelaAoEncerrar(CancellationTokenSource cancelamento) : IGravador
    {
        public OperationCanceledException Excecao { get; } =
            new("StopRecord cancelado.", cancelamento.Token);

        public Task IniciarAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string> FinalizarAsync(CancellationToken cancellationToken)
        {
            cancelamento.Cancel();
            return Task.FromException<string>(Excecao);
        }

        public Task<bool> EstaGravandoAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class GravadorComFalha : IGravador
    {
        public Task IniciarAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("OBS indisponível.");
        public Task<string> FinalizarAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> EstaGravandoAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    }

    private sealed class WorkerLauncherFake(List<string>? eventos = null, bool falhar = false) : IWorkerLauncher
    {
        public bool Iniciou { get; private set; }

        public Task IniciarAsync(CancellationToken cancellationToken)
        {
            Iniciou = true;
            eventos?.Add("worker");
            return falhar
                ? Task.FromException(new InvalidOperationException("Worker indisponível."))
                : Task.CompletedTask;
        }
    }

    private sealed class EventoSinkFake : IEventoOperacionalSink
    {
        public List<EventoOperacional> Eventos { get; } = [];

        public Task RegistrarAsync(EventoOperacional evento, CancellationToken cancellationToken)
        {
            Eventos.Add(evento);
            return Task.CompletedTask;
        }

        public Task RemoverAnterioresAsync(DateTimeOffset limiteUtc, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
