using Anamnesis.Application.Modelos;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Anamnesis.Infrastructure.Persistencia;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class SqliteReuniaoQueryTests : IAsyncLifetime
{
    private readonly string _caminhoBanco = Path.Combine(
        Path.GetTempPath(),
        $"anamnesis-query-{Guid.NewGuid():N}.db");

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
    public async Task DeveListarCemMaisRecentesComBuscaEFiltroSemCarregarConteudo()
    {
        var repository = new SqliteReuniaoRepository(_caminhoBanco);
        var antiga = CriarAguardandoProcessamento("Planejamento antigo", new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));
        var recente = CriarArquivada("Planejamento recente", new DateTimeOffset(2026, 8, 5, 14, 0, 0, TimeSpan.Zero));
        var outra = CriarAguardandoProcessamento("Entrevista", new DateTimeOffset(2026, 8, 6, 9, 0, 0, TimeSpan.Zero));
        await repository.SalvarAsync(antiga, CancellationToken.None);
        await repository.SalvarAsync(recente, CancellationToken.None);
        await repository.SalvarAsync(outra, CancellationToken.None);
        var query = new SqliteReuniaoQuery(_caminhoBanco);

        var resultado = await query.ListarAsync(
            new ReuniaoQueryFiltro("planejamento", StatusReuniao.Arquivada, 100),
            CancellationToken.None);

        var resumo = Assert.Single(resultado);
        Assert.Equal(recente.Id, resumo.Id);
        Assert.Equal("Planejamento recente", resumo.Titulo);
        Assert.Equal(StatusReuniao.Arquivada, resumo.Status);
        Assert.Equal(recente.CriadaEm, resumo.CriadaEm);
        Assert.NotNull(resumo.GravacaoIniciadaEm);
        Assert.NotNull(resumo.GravacaoFinalizadaEm);
    }

    [Fact]
    public async Task DeveOrdenarPorCriacaoDescendenteEAplicarLimite()
    {
        var repository = new SqliteReuniaoRepository(_caminhoBanco);
        var primeira = CriarAguardandoProcessamento("Primeira", new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero));
        var segunda = CriarAguardandoProcessamento("Segunda", new DateTimeOffset(2026, 8, 2, 10, 0, 0, TimeSpan.Zero));
        var terceira = CriarAguardandoProcessamento("Terceira", new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero));
        await repository.SalvarAsync(primeira, CancellationToken.None);
        await repository.SalvarAsync(terceira, CancellationToken.None);
        await repository.SalvarAsync(segunda, CancellationToken.None);
        var query = new SqliteReuniaoQuery(_caminhoBanco);

        var resultado = await query.ListarAsync(
            new ReuniaoQueryFiltro(null, null, 2),
            CancellationToken.None);

        Assert.Equal([terceira.Id, segunda.Id], resultado.Select(item => item.Id));
    }

    [Fact]
    public async Task DeveCarregarDetalheEstruturadoSomenteSobDemanda()
    {
        var reuniao = CriarArquivada("Revisao detalhada", new DateTimeOffset(2026, 8, 5, 16, 0, 0, TimeSpan.Zero));
        await new SqliteReuniaoRepository(_caminhoBanco).SalvarAsync(reuniao, CancellationToken.None);
        var artefatosEsperados = new ArtefatosReuniao(
            reuniao.Id,
            @"C:\arquivo\2026\08\reuniao",
            @"C:\arquivo\2026\08\reuniao\ata.md",
            @"C:\arquivo\2026\08\reuniao\transcricao.md");
        await new SqliteArtefatoRepository(_caminhoBanco)
            .SalvarAsync(artefatosEsperados, CancellationToken.None);
        var query = new SqliteReuniaoQuery(_caminhoBanco);

        var detalhe = await query.ObterDetalheAsync(reuniao.Id, CancellationToken.None);

        Assert.NotNull(detalhe);
        Assert.Equal(reuniao.Id, detalhe!.Id);
        Assert.Equal(@"C:\gravacoes\reuniao.mkv", detalhe.CaminhoGravacao);
        Assert.Equal("Transcricao local.", detalhe.TextoTranscricao);
        Assert.Equal("Resumo estruturado.", detalhe.ResumoExecutivo);
        Assert.Equal(["Aprovar a proposta."], detalhe.Decisoes);
        var tarefa = Assert.Single(detalhe.Tarefas);
        Assert.Equal("Enviar proposta.", tarefa.Descricao);
        Assert.Equal(artefatosEsperados, detalhe.Artefatos);
    }

    [Fact]
    public async Task DeveRejeitarLimiteForaDoIntervaloUmACem()
    {
        var query = new SqliteReuniaoQuery(_caminhoBanco);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => query.ListarAsync(
            new ReuniaoQueryFiltro(null, null, 101),
            CancellationToken.None));
    }

    private static Reuniao CriarAguardandoProcessamento(string titulo, DateTimeOffset criadaEm)
    {
        var reuniao = new Reuniao(Guid.NewGuid(), titulo, criadaEm);
        reuniao.IniciarGravacao(criadaEm.AddMinutes(1));
        reuniao.FinalizarGravacao(@"C:\gravacoes\reuniao.mkv", criadaEm.AddMinutes(31));
        return reuniao;
    }

    private static Reuniao CriarArquivada(string titulo, DateTimeOffset criadaEm)
    {
        var reuniao = CriarAguardandoProcessamento(titulo, criadaEm);
        reuniao.IniciarTranscricao();
        reuniao.RegistrarTranscricao(new Transcricao("Transcricao local.", "pt-BR", criadaEm.AddMinutes(32)));
        reuniao.RegistrarAta(new Ata(
            "Resumo estruturado.",
            ["Aprovar a proposta."],
            [new Tarefa("Enviar proposta.", "Felipe", new DateOnly(2026, 8, 8))],
            criadaEm.AddMinutes(33)));
        reuniao.MarcarArquivada(criadaEm.AddMinutes(34));
        return reuniao;
    }
}
