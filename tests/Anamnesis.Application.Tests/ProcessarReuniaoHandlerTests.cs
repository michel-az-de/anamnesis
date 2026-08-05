using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
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
}
