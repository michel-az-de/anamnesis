using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Anamnesis.Application.UseCases;
using Anamnesis.Domain.Entidades;
using Anamnesis.Worker;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class ReuniaoConsumerTests
{
    [Fact]
    public async Task DeveConcluirJobDepoisDeProcessarReuniao()
    {
        var reuniao = CriarReuniao();
        var job = CriarJob(reuniao.Id);
        var fila = new JobQueueFake(job);
        var consumer = CriarConsumer(fila, reuniao, new TranscritorFake());

        var processou = await consumer.ProcessarProximoAsync(CancellationToken.None);

        Assert.True(processou);
        Assert.Equal(job.Id, fila.JobConcluidoId);
        Assert.Null(fila.JobLiberadoId);
    }

    [Fact]
    public async Task NaoDeveProcessarQuandoNaoHaJob()
    {
        var fila = new JobQueueFake(null);
        var consumer = CriarConsumer(fila, CriarReuniao(), new TranscritorFake());

        var processou = await consumer.ProcessarProximoAsync(CancellationToken.None);

        Assert.False(processou);
        Assert.Null(fila.JobConcluidoId);
    }

    [Fact]
    public async Task DeveLiberarJobQuandoOProcessamentoFalha()
    {
        var reuniao = CriarReuniao();
        var job = CriarJob(reuniao.Id);
        var fila = new JobQueueFake(job);
        var consumer = CriarConsumer(fila, reuniao, new TranscritorComFalha());

        var excecao = await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.ProcessarProximoAsync(CancellationToken.None));

        Assert.Equal("Whisper indisponível.", excecao.Message);
        Assert.Equal(job.Id, fila.JobLiberadoId);
        Assert.Null(fila.JobConcluidoId);
    }

    [Fact]
    public async Task DeveRegistrarReservaEConclusaoComAmbasCorrelacoes()
    {
        var reuniao = CriarReuniao();
        var job = CriarJob(reuniao.Id);
        var fila = new JobQueueFake(job);
        var sink = new EventoSinkFake();
        var journal = new JornalOperacional(sink, TimeProvider.System);
        var consumer = CriarConsumer(fila, reuniao, new TranscritorFake(), journal);

        await consumer.ProcessarProximoAsync(CancellationToken.None);

        var marcosWorker = sink.Eventos.Where(evento => evento.Codigo is
            CodigosEventoOperacional.JobReservado or
            CodigosEventoOperacional.JobConcluido).ToArray();
        Assert.Equal(
            [CodigosEventoOperacional.JobReservado, CodigosEventoOperacional.JobConcluido],
            marcosWorker.Select(evento => evento.Codigo));
        Assert.All(marcosWorker, evento =>
        {
            Assert.Equal(reuniao.Id, evento.ReuniaoId);
            Assert.Equal(job.Id, evento.JobId);
        });
    }

    private static ReuniaoConsumer CriarConsumer(
        JobQueueFake fila,
        Reuniao reuniao,
        ITranscritor transcritor,
        JornalOperacional? journal = null) =>
        new(
            fila,
            new ProcessarReuniaoHandler(
                new ReuniaoRepositoryFake(reuniao),
                transcritor,
                new AtaRunnerFake(),
                new ArquivadorFake(),
                new ArtefatoRepositoryFake(),
                TimeProvider.System,
                journal),
            TimeProvider.System,
            journal);

    private static Reuniao CriarReuniao()
    {
        var reuniao = new Reuniao(Guid.NewGuid(), "Planejamento", DateTimeOffset.UtcNow);
        reuniao.IniciarGravacao(DateTimeOffset.UtcNow);
        reuniao.FinalizarGravacao("C:\\gravacoes\\planejamento.mkv", DateTimeOffset.UtcNow);
        return reuniao;
    }

    private static JobProcessamento CriarJob(Guid reuniaoId) =>
        new(Guid.NewGuid(), reuniaoId, DateTimeOffset.UtcNow, null, 0);

    private sealed class JobQueueFake(JobProcessamento? job) : IJobQueue
    {
        private JobProcessamento? _job = job;

        public Guid? JobConcluidoId { get; private set; }
        public Guid? JobLiberadoId { get; private set; }

        public Task<JobProcessamento> EnfileirarAsync(Guid reuniaoId, DateTimeOffset criadoEm, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<JobProcessamento?> ReservarProximoAsync(DateTimeOffset reservadoEm, CancellationToken cancellationToken)
        {
            var reservado = _job is null ? null : _job with { ReservadoEm = reservadoEm, Tentativas = _job.Tentativas + 1 };
            _job = null;
            return Task.FromResult(reservado);
        }

        public Task LiberarAsync(Guid jobId, CancellationToken cancellationToken)
        {
            JobLiberadoId = jobId;
            return Task.CompletedTask;
        }

        public Task ConcluirAsync(Guid jobId, DateTimeOffset concluidoEm, CancellationToken cancellationToken)
        {
            JobConcluidoId = jobId;
            return Task.CompletedTask;
        }

        public Task LiberarReservasAtivasAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ReuniaoRepositoryFake(Reuniao reuniao) : IReuniaoRepository
    {
        public Task<Reuniao?> ObterAsync(Guid reuniaoId, CancellationToken cancellationToken) => Task.FromResult<Reuniao?>(reuniao);
        public Task SalvarAsync(Reuniao reuniao, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TranscritorFake : ITranscritor
    {
        public Task<TranscricaoGerada> TranscreverAsync(string caminhoArquivo, CancellationToken cancellationToken) =>
            Task.FromResult(new TranscricaoGerada("Transcrição.", "pt-BR"));
    }

    private sealed class TranscritorComFalha : ITranscritor
    {
        public Task<TranscricaoGerada> TranscreverAsync(string caminhoArquivo, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Whisper indisponível.");
    }

    private sealed class AtaRunnerFake : IAtaRunner
    {
        public string Nome => "Fake";
        public Task<AtaGerada> GerarAsync(Reuniao reuniao, TranscricaoGerada transcricao, CancellationToken cancellationToken) =>
            Task.FromResult(new AtaGerada("Resumo.", [], []));
    }

    private sealed class ArquivadorFake : IArquivador
    {
        public Task<ArtefatosReuniao> ArquivarAsync(Reuniao reuniao, CancellationToken cancellationToken) =>
            Task.FromResult(new ArtefatosReuniao(
                reuniao.Id,
                @"C:\arquivo\reuniao",
                @"C:\arquivo\reuniao\ata.md",
                @"C:\arquivo\reuniao\transcricao.md"));
    }

    private sealed class ArtefatoRepositoryFake : IArtefatoRepository
    {
        public Task SalvarAsync(ArtefatosReuniao artefatos, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<ArtefatosReuniao?> ObterAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult<ArtefatosReuniao?>(null);
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
