using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
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
    public async Task DeveRegistrarFalhaSemEnfileirarJob()
    {
        var repository = new ReuniaoRepositoryFake();
        var fila = new JobQueueFake();
        var handler = CriarHandler(repository, fila, new GravadorComFalha());

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.IniciarAsync("Teste", CancellationToken.None));

        var reuniao = Assert.Single(repository.Salvas);
        Assert.Equal("OBS indisponível.", excecao.Message);
        Assert.Equal(StatusReuniao.Falha, reuniao.Status);
        Assert.Null(fila.ReuniaoEnfileiradaId);
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

    private static ControlarGravacaoHandler CriarHandler(
        IReuniaoRepository repository,
        JobQueueFake fila,
        IGravador gravador,
        IWorkerLauncher? workerLauncher = null,
        IObsPreflight? obsPreflight = null) =>
        new(
            repository,
            fila,
            gravador,
            workerLauncher ?? new WorkerLauncherFake(),
            obsPreflight ?? new ObsPreflightFake(),
            TimeProvider.System);

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

        public Task<Reuniao?> ObterAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult(_reunioes.GetValueOrDefault(reuniaoId));

        public Task SalvarAsync(Reuniao reuniao, CancellationToken cancellationToken)
        {
            _reunioes[reuniao.Id] = reuniao;
            if (!Salvas.Contains(reuniao))
            {
                Salvas.Add(reuniao);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class JobQueueFake(List<string>? eventos = null) : IJobQueue
    {
        public Guid? ReuniaoEnfileiradaId { get; private set; }

        public Task<JobProcessamento> EnfileirarAsync(Guid reuniaoId, DateTimeOffset criadoEm, CancellationToken cancellationToken)
        {
            ReuniaoEnfileiradaId = reuniaoId;
            eventos?.Add("job");
            return Task.FromResult(new JobProcessamento(Guid.NewGuid(), reuniaoId, criadoEm, null, 0));
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
}
