using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Anamnesis.Infrastructure.Persistencia;
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
}
