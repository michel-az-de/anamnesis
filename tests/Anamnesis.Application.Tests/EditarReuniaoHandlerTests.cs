using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.UseCases;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Xunit;

namespace Anamnesis.Application.Tests;

public sealed class EditarReuniaoHandlerTests
{
    [Fact]
    public async Task DevePersistirEdicaoEAtualizarArtefatosDeReuniaoArquivada()
    {
        var reuniao = CriarArquivada();
        var repositorio = new ReuniaoRepositoryFake(reuniao);
        var arquivador = new ArquivadorFake();
        var handler = new EditarReuniaoHandler(repositorio, arquivador);

        await handler.ExecutarAsync(
            reuniao.Id,
            "  Revisão final  ",
            "Transcrição corrigida em UTF-8: ação.",
            CancellationToken.None);

        Assert.Equal("Revisão final", repositorio.Reuniao.Titulo);
        Assert.Equal("Transcrição corrigida em UTF-8: ação.", repositorio.Reuniao.Transcricao!.Texto);
        Assert.Equal(1, repositorio.Salvamentos);
        Assert.Same(repositorio.Reuniao, arquivador.UltimaReuniao);
    }

    [Fact]
    public async Task NaoDeveRearquivarReuniaoQueAindaEstaEmAnalise()
    {
        var agora = DateTimeOffset.UtcNow;
        var reuniao = Reuniao.Reconstituir(
            Guid.NewGuid(),
            "Em análise",
            agora,
            StatusReuniao.EmAnalise,
            new Gravacao("C:\\gravacoes\\analise.mkv", agora, agora),
            new Transcricao("Original", "pt", agora),
            null,
            null);
        var repositorio = new ReuniaoRepositoryFake(reuniao);
        var arquivador = new ArquivadorFake();
        var handler = new EditarReuniaoHandler(repositorio, arquivador);

        await handler.ExecutarAsync(reuniao.Id, "Novo título", "Novo texto", CancellationToken.None);

        Assert.Equal(1, repositorio.Salvamentos);
        Assert.Null(arquivador.UltimaReuniao);
    }

    private static Reuniao CriarArquivada()
    {
        var agora = DateTimeOffset.UtcNow;
        return Reuniao.Reconstituir(
            Guid.NewGuid(),
            "Original",
            agora,
            StatusReuniao.Arquivada,
            new Gravacao("C:\\gravacoes\\original.mkv", agora, agora),
            new Transcricao("Original", "pt", agora),
            new Ata("Resumo", [], [], agora),
            null,
            agora);
    }

    private sealed class ReuniaoRepositoryFake(Reuniao reuniao) : IReuniaoRepository
    {
        public Reuniao Reuniao { get; private set; } = reuniao;
        public int Salvamentos { get; private set; }

        public Task<Reuniao?> ObterAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult<Reuniao?>(Reuniao.Id == reuniaoId ? Reuniao : null);

        public Task SalvarAsync(Reuniao atualizada, CancellationToken cancellationToken)
        {
            Reuniao = atualizada;
            Salvamentos++;
            return Task.CompletedTask;
        }
    }

    private sealed class ArquivadorFake : IArquivador
    {
        public Reuniao? UltimaReuniao { get; private set; }

        public Task<ArtefatosReuniao> ArquivarAsync(
            Reuniao reuniao,
            CancellationToken cancellationToken)
        {
            UltimaReuniao = reuniao;
            return Task.FromResult(new ArtefatosReuniao(
                reuniao.Id,
                "arquivo",
                "arquivo/ata.md",
                "arquivo/transcricao.md"));
        }
    }
}
