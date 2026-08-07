using Anamnesis.Application.Modelos;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Anamnesis.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
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

    [Theory]
    [InlineData("orçamento reservado", "Resumo", "orçamento reservado")]
    [InlineData("contrato aprovado", "Decisões", "contrato aprovado")]
    [InlineData("enviar proposta", "Tarefas", "Enviar proposta")]
    [InlineData("incidente resolvido", "Transcrição", "incidente resolvido")]
    public async Task DeveBuscarTodoConteudoERetornarSecaoETrecho(
        string texto,
        string secaoEsperada,
        string trechoEsperado)
    {
        var reuniao = CriarArquivadaComConteudo(
            "Reunião sem o termo pesquisado no título",
            new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero));
        await new SqliteReuniaoRepository(_caminhoBanco)
            .SalvarAsync(reuniao, CancellationToken.None);

        var resultado = await new SqliteReuniaoQuery(_caminhoBanco).ListarAsync(
            new ReuniaoQueryFiltro(texto, StatusReuniao.Arquivada, 100),
            CancellationToken.None);

        var resumo = Assert.Single(resultado);
        Assert.Equal(secaoEsperada, resumo.SecaoCorrespondente);
        Assert.Contains(trechoEsperado, resumo.TrechoCorrespondente, StringComparison.OrdinalIgnoreCase);
        Assert.True(resumo.TrechoCorrespondente!.Length <= 180);
    }

    [Fact]
    public async Task DeveCombinarBuscaComPeriodo()
    {
        var antiga = CriarArquivadaComConteudo(
            "Reunião antiga",
            new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero));
        var recente = CriarArquivadaComConteudo(
            "Reunião recente",
            new DateTimeOffset(2026, 8, 7, 10, 0, 0, TimeSpan.Zero));
        var repository = new SqliteReuniaoRepository(_caminhoBanco);
        await repository.SalvarAsync(antiga, CancellationToken.None);
        await repository.SalvarAsync(recente, CancellationToken.None);

        var resultado = await new SqliteReuniaoQuery(_caminhoBanco).ListarAsync(
            new ReuniaoQueryFiltro(
                "incidente resolvido",
                StatusReuniao.Arquivada,
                100,
                new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)),
            CancellationToken.None);

        Assert.Equal([recente.Id], resultado.Select(item => item.Id));
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
    public async Task DeveNormalizarUtcAntesDeOrdenarPorCriacao()
    {
        var repository = new SqliteReuniaoRepository(_caminhoBanco);
        var maisRecente = CriarAguardandoProcessamento(
            "Mais recente",
            new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero));
        var maisAntigaComHoraLocalMaior = CriarAguardandoProcessamento(
            "Mais antiga",
            new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.FromHours(3)));
        await repository.SalvarAsync(maisRecente, CancellationToken.None);
        await repository.SalvarAsync(maisAntigaComHoraLocalMaior, CancellationToken.None);

        var resultado = await new SqliteReuniaoQuery(_caminhoBanco).ListarAsync(
            new ReuniaoQueryFiltro(null, null, 100),
            CancellationToken.None);

        Assert.Equal([maisRecente.Id, maisAntigaComHoraLocalMaior.Id], resultado.Select(item => item.Id));
        await using var conexao = new SqliteConnection($"Data Source={_caminhoBanco};Pooling=False");
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT criada_em FROM reunioes WHERE id = $id;";
        comando.Parameters.AddWithValue("$id", maisAntigaComHoraLocalMaior.Id.ToString("N"));
        var instantePersistido = Assert.IsType<string>(await comando.ExecuteScalarAsync());
        Assert.Equal("2026-08-05T09:00:00.0000000+00:00", instantePersistido);
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

    private static Reuniao CriarArquivadaComConteudo(string titulo, DateTimeOffset criadaEm)
    {
        var reuniao = CriarAguardandoProcessamento(titulo, criadaEm);
        reuniao.IniciarTranscricao();
        reuniao.RegistrarTranscricao(new Transcricao(
            "A equipe analisou o cenário e declarou o incidente resolvido com segurança.",
            "pt-BR",
            criadaEm.AddMinutes(32)));
        reuniao.RegistrarAta(new Ata(
            "O orçamento reservado permite concluir o trabalho neste ciclo.",
            ["O contrato aprovado será usado como referência."],
            [new Tarefa("Enviar proposta revisada.", "Felipe", new DateOnly(2026, 8, 12))],
            criadaEm.AddMinutes(33)));
        reuniao.MarcarArquivada(criadaEm.AddMinutes(34));
        return reuniao;
    }
}
