using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Anamnesis.Application.UseCases;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Xunit;

namespace Anamnesis.Application.Tests;

public sealed class ProcessarReuniaoHandlerTests
{
    [Fact]
    public async Task DeveArquivarReuniaoAposGerarTranscricaoEAta()
    {
        var reuniao = new Reuniao(Guid.NewGuid(), "Planejamento", DateTimeOffset.UtcNow);
        reuniao.IniciarGravacao(DateTimeOffset.UtcNow);
        reuniao.FinalizarGravacao("C:\\gravacoes\\planejamento.mkv", DateTimeOffset.UtcNow);

        var repository = new ReuniaoRepositoryFake(reuniao);
        var artefatoRepository = new ArtefatoRepositoryFake();
        var handler = new ProcessarReuniaoHandler(
            repository,
            new TranscritorFake(),
            new AtaRunnerFake(),
            new ArquivadorFake(),
            artefatoRepository,
            TimeProvider.System);

        await handler.ExecutarAsync(reuniao.Id, CancellationToken.None);

        Assert.Equal(StatusReuniao.Arquivada, reuniao.Status);
        Assert.NotNull(reuniao.Ata);
        Assert.Equal("Resumo da reunião.", reuniao.Ata!.ResumoExecutivo);
        Assert.Equal(reuniao.Id, Assert.Single(artefatoRepository.Salvos).ReuniaoId);
    }

    [Fact]
    public async Task DeveReprocessarReuniaoEmFalha()
    {
        var reuniao = new Reuniao(Guid.NewGuid(), "Retentativa", DateTimeOffset.UtcNow);
        reuniao.IniciarGravacao(DateTimeOffset.UtcNow);
        reuniao.FinalizarGravacao("C:\\gravacoes\\retentativa.mkv", DateTimeOffset.UtcNow);
        reuniao.RegistrarFalha("Whisper indisponível");
        var handler = new ProcessarReuniaoHandler(
            new ReuniaoRepositoryFake(reuniao),
            new TranscritorFake(),
            new AtaRunnerFake(),
            new ArquivadorFake(),
            new ArtefatoRepositoryFake(),
            TimeProvider.System);

        await handler.ExecutarAsync(reuniao.Id, CancellationToken.None);

        Assert.Equal(StatusReuniao.Arquivada, reuniao.Status);
    }

    [Fact]
    public async Task DeveCorrelacionarEtapasDeProcessamentoComJob()
    {
        var reuniao = new Reuniao(Guid.NewGuid(), "Observável", DateTimeOffset.UtcNow);
        reuniao.IniciarGravacao(DateTimeOffset.UtcNow);
        reuniao.FinalizarGravacao(@"C:\gravacoes\observavel.mkv", DateTimeOffset.UtcNow);
        var jobId = Guid.NewGuid();
        var sink = new EventoSinkFake();
        var handler = new ProcessarReuniaoHandler(
            new ReuniaoRepositoryFake(reuniao),
            new TranscritorFake(),
            new AtaRunnerFake(),
            new ArquivadorFake(),
            new ArtefatoRepositoryFake(),
            TimeProvider.System,
            new JornalOperacional(sink, TimeProvider.System));

        await handler.ExecutarAsync(reuniao.Id, jobId, CancellationToken.None);

        Assert.Equal(
            [
                CodigosEventoOperacional.TranscricaoIniciada,
                CodigosEventoOperacional.TranscricaoConcluida,
                CodigosEventoOperacional.AtaGerada,
                CodigosEventoOperacional.ReuniaoArquivada
            ],
            sink.Eventos.Select(evento => evento.Codigo));
        Assert.All(sink.Eventos, evento =>
        {
            Assert.Equal(reuniao.Id, evento.ReuniaoId);
            Assert.Equal(jobId, evento.JobId);
        });
    }

    [Fact]
    public async Task FalhaDoWhisperDeveIdentificarAEtapaSemPersistirMensagemLivre()
    {
        var reuniao = new Reuniao(Guid.NewGuid(), "Falha observável", DateTimeOffset.UtcNow);
        reuniao.IniciarGravacao(DateTimeOffset.UtcNow);
        reuniao.FinalizarGravacao(@"C:\gravacoes\falha.mkv", DateTimeOffset.UtcNow);
        var jobId = Guid.NewGuid();
        var sink = new EventoSinkFake();
        var handler = new ProcessarReuniaoHandler(
            new ReuniaoRepositoryFake(reuniao),
            new TranscritorComFalha(),
            new AtaRunnerFake(),
            new ArquivadorFake(),
            new ArtefatoRepositoryFake(),
            TimeProvider.System,
            new JornalOperacional(sink, TimeProvider.System));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.ExecutarAsync(reuniao.Id, jobId, CancellationToken.None));

        var falha = sink.Eventos.Last();
        Assert.Equal(CodigosEventoOperacional.OperacaoFalhou, falha.Codigo);
        Assert.Equal("Whisper", falha.Componente);
        Assert.Equal("transcrever", falha.Metadados.Operacao);
        Assert.Equal(nameof(InvalidOperationException), falha.Metadados.MotivoCodigo);
        Assert.DoesNotContain("conteúdo confidencial", falha.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ReuniaoRepositoryFake(Reuniao reuniao) : IReuniaoRepository
    {
        public Task<Reuniao?> ObterAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult<Reuniao?>(reuniao.Id == reuniaoId ? reuniao : null);

        public Task SalvarAsync(Reuniao reuniao, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TranscritorFake : ITranscritor
    {
        public Task<TranscricaoGerada> TranscreverAsync(string caminhoArquivo, CancellationToken cancellationToken) =>
            Task.FromResult(new TranscricaoGerada("Felipe prepara a proposta.", "pt"));
    }

    private sealed class AtaRunnerFake : IAtaRunner
    {
        public string Nome => "Fake";

        public Task<AtaGerada> GerarAsync(Reuniao reuniao, TranscricaoGerada transcricao, CancellationToken cancellationToken) =>
            Task.FromResult(new AtaGerada(
                "Resumo da reunião.",
                ["Enviar proposta."],
                [new Tarefa("Preparar proposta.", "Felipe", null)]));
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
        public List<ArtefatosReuniao> Salvos { get; } = [];

        public Task SalvarAsync(ArtefatosReuniao artefatos, CancellationToken cancellationToken)
        {
            Salvos.Add(artefatos);
            return Task.CompletedTask;
        }

        public Task<ArtefatosReuniao?> ObterAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult(Salvos.SingleOrDefault(item => item.ReuniaoId == reuniaoId));
    }

    private sealed class TranscritorComFalha : ITranscritor
    {
        public Task<TranscricaoGerada> TranscreverAsync(string caminhoArquivo, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("conteúdo confidencial retornado pelo Whisper");
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
