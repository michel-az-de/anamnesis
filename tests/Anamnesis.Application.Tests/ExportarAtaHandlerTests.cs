using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.UseCases;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Xunit;

namespace Anamnesis.Application.Tests;

public sealed class ExportarAtaHandlerTests
{
    [Fact]
    public async Task DeveExportarAtaGeradaSemAlterarAReuniao()
    {
        var detalhe = CriarDetalheComAta();
        var exportador = new ExportadorFake();
        var handler = new ExportarAtaHandler(new ReuniaoQueryFake(detalhe), exportador);

        var resultado = await handler.ExecutarAsync(
            detalhe.Id,
            FormatoExportacaoAta.Pdf,
            @"C:\destino\ata.pdf",
            sobrescrever: true,
            CancellationToken.None);

        Assert.Equal(@"C:\destino\ata.pdf", resultado);
        Assert.Equal(FormatoExportacaoAta.Pdf, exportador.Formato);
        Assert.True(exportador.Sobrescrever);
        Assert.Same(detalhe, exportador.Detalhe);
    }

    [Fact]
    public async Task DeveRejeitarReuniaoSemAta()
    {
        var detalhe = CriarDetalheComAta() with
        {
            ResumoExecutivo = null,
            AtaGeradaEm = null,
            Decisoes = [],
            Tarefas = []
        };
        var exportador = new ExportadorFake();
        var handler = new ExportarAtaHandler(new ReuniaoQueryFake(detalhe), exportador);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecutarAsync(
            detalhe.Id,
            FormatoExportacaoAta.Docx,
            @"C:\destino\ata.docx",
            sobrescrever: false,
            CancellationToken.None));

        Assert.Contains("ata", erro.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(exportador.Detalhe);
    }

    [Fact]
    public async Task DevePublicarAtaNoObsidianPeloCasoDeUso()
    {
        var detalhe = CriarDetalheComAta();
        var publicador = new PublicadorObsidianFake();
        var handler = new PublicarAtaObsidianHandler(new ReuniaoQueryFake(detalhe), publicador);

        var resultado = await handler.ExecutarAsync(
            detalhe.Id,
            @"C:\vault",
            "Anamnesis/Reunioes",
            CancellationToken.None);

        Assert.Equal(Path.Combine(@"C:\vault", "Anamnesis/Reunioes", "ata.md"), resultado);
        Assert.Same(detalhe, publicador.Detalhe);
    }

    private static ReuniaoDetalhe CriarDetalheComAta()
    {
        var id = Guid.NewGuid();
        var inicio = new DateTimeOffset(2026, 8, 7, 14, 0, 0, TimeSpan.Zero);
        return new ReuniaoDetalhe(
            id,
            "Planejamento da entrega",
            inicio,
            StatusReuniao.Arquivada,
            null,
            @"C:\gravacoes\reuniao.mkv",
            inicio,
            inicio.AddMinutes(45),
            "Transcrição local.",
            "pt-BR",
            inicio.AddMinutes(46),
            "A equipe confirmou a próxima entrega.",
            ["Publicar a versão revisada."],
            [new Tarefa("Enviar o relatório.", "Felipe", new DateOnly(2026, 8, 10))],
            inicio.AddMinutes(47),
            inicio.AddMinutes(48),
            null);
    }

    private sealed class ReuniaoQueryFake(ReuniaoDetalhe? detalhe) : IReuniaoQuery
    {
        public Task<IReadOnlyList<ReuniaoResumo>> ListarAsync(
            ReuniaoQueryFiltro filtro,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReuniaoResumo>>([]);

        public Task<ReuniaoDetalhe?> ObterDetalheAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult(detalhe is not null && detalhe.Id == reuniaoId ? detalhe : null);
    }

    private sealed class ExportadorFake : IExportadorAta
    {
        public ReuniaoDetalhe? Detalhe { get; private set; }
        public FormatoExportacaoAta? Formato { get; private set; }
        public bool Sobrescrever { get; private set; }

        public Task<string> ExportarAsync(
            ReuniaoDetalhe detalhe,
            FormatoExportacaoAta formato,
            string caminhoDestino,
            bool sobrescrever,
            CancellationToken cancellationToken)
        {
            Detalhe = detalhe;
            Formato = formato;
            Sobrescrever = sobrescrever;
            return Task.FromResult(caminhoDestino);
        }
    }

    private sealed class PublicadorObsidianFake : IPublicadorObsidian
    {
        public ReuniaoDetalhe? Detalhe { get; private set; }

        public Task<string> PublicarAsync(
            ReuniaoDetalhe detalhe,
            string caminhoVault,
            string subpasta,
            CancellationToken cancellationToken)
        {
            Detalhe = detalhe;
            return Task.FromResult(Path.Combine(caminhoVault, subpasta, "ata.md"));
        }
    }
}
