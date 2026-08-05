using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.UseCases;
using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Xunit;

namespace Anamnesis.Application.Tests;

public sealed class RetencaoGravacaoHandlerTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DeveSimularSemMoverGravacao()
    {
        var reuniao = CriarReuniaoArquivada(Agora.AddDays(-7));
        var lixeira = new LixeiraFake(existe: true);
        var handler = CriarHandler(reuniao, lixeira);

        var resultado = await handler.SimularAsync(reuniao.Id, CancellationToken.None);

        Assert.True(resultado.PodeMover);
        Assert.Equal(reuniao.Gravacao!.CaminhoArquivo, resultado.CaminhoArquivo);
        Assert.Null(lixeira.CaminhoMovido);
        Assert.Equal(StatusReuniao.Arquivada, reuniao.Status);
    }

    [Fact]
    public async Task DeveMoverParaLixeiraDepoisDeSeteDias()
    {
        var reuniao = CriarReuniaoArquivada(Agora.AddDays(-7));
        var repository = new ReuniaoRepositoryFake(reuniao);
        var lixeira = new LixeiraFake(existe: true);
        var handler = new RetencaoGravacaoHandler(repository, lixeira, new RelogioFixo(Agora));

        await handler.AplicarAsync(reuniao.Id, CancellationToken.None);

        Assert.Equal(reuniao.Gravacao!.CaminhoArquivo, lixeira.CaminhoMovido);
        Assert.Equal(StatusReuniao.Excluida, reuniao.Status);
        Assert.Equal([StatusReuniao.PendenteExclusao, StatusReuniao.Excluida], repository.StatusSalvos);
    }

    [Fact]
    public async Task DeveFalharQuandoGravacaoNaoExiste()
    {
        var reuniao = CriarReuniaoArquivada(Agora.AddDays(-7));
        var lixeira = new LixeiraFake(existe: false);
        var handler = CriarHandler(reuniao, lixeira);

        var excecao = await Assert.ThrowsAsync<FileNotFoundException>(() => handler.AplicarAsync(reuniao.Id, CancellationToken.None));

        Assert.Equal(reuniao.Gravacao!.CaminhoArquivo, excecao.FileName);
        Assert.Equal(StatusReuniao.Arquivada, reuniao.Status);
        Assert.Null(lixeira.CaminhoMovido);
    }

    [Fact]
    public async Task DeveRestaurarEstadoArquivadoQuandoLixeiraFalha()
    {
        var reuniao = CriarReuniaoArquivada(Agora.AddDays(-7));
        var repository = new ReuniaoRepositoryFake(reuniao);
        var lixeira = new LixeiraFake(existe: true, falharAoMover: true);
        var handler = new RetencaoGravacaoHandler(repository, lixeira, new RelogioFixo(Agora));

        await Assert.ThrowsAsync<IOException>(() => handler.AplicarAsync(reuniao.Id, CancellationToken.None));

        Assert.Equal(StatusReuniao.Arquivada, reuniao.Status);
        Assert.Equal([StatusReuniao.PendenteExclusao, StatusReuniao.Arquivada], repository.StatusSalvos);
    }

    [Fact]
    public async Task DeveRelatarMotivoTipadoSemDependerDoTexto()
    {
        var reuniao = CriarReuniaoArquivada(Agora.AddDays(-1));
        var handler = CriarHandler(reuniao, new LixeiraFake(existe: true));

        var resultado = await handler.SimularAsync(reuniao.Id, CancellationToken.None);

        Assert.Equal(MotivoRetencao.PrazoNaoAtingido, resultado.Motivo);
        Assert.False(resultado.PodeMover);
        Assert.Equal("O prazo de retenção ainda não foi atingido.", resultado.Descricao);
    }

    private static RetencaoGravacaoHandler CriarHandler(Reuniao reuniao, LixeiraFake lixeira) =>
        new(new ReuniaoRepositoryFake(reuniao), lixeira, new RelogioFixo(Agora));

    private static Reuniao CriarReuniaoArquivada(DateTimeOffset arquivadaEm)
    {
        var reuniao = new Reuniao(Guid.NewGuid(), "Planejamento", arquivadaEm.AddHours(-1));
        reuniao.IniciarGravacao(arquivadaEm.AddMinutes(-50));
        reuniao.FinalizarGravacao("C:\\gravacoes\\planejamento.mkv", arquivadaEm.AddMinutes(-40));
        reuniao.IniciarTranscricao();
        reuniao.RegistrarTranscricao(new Transcricao("Texto.", "pt-BR", arquivadaEm.AddMinutes(-30)));
        reuniao.RegistrarAta(new Ata("Resumo.", [], [], arquivadaEm.AddMinutes(-20)));
        reuniao.MarcarArquivada(arquivadaEm);
        return reuniao;
    }

    private sealed class ReuniaoRepositoryFake(Reuniao reuniao) : IReuniaoRepository
    {
        public List<StatusReuniao> StatusSalvos { get; } = [];

        public Task<Reuniao?> ObterAsync(Guid reuniaoId, CancellationToken cancellationToken) =>
            Task.FromResult<Reuniao?>(reuniao.Id == reuniaoId ? reuniao : null);

        public Task SalvarAsync(Reuniao reuniao, CancellationToken cancellationToken)
        {
            StatusSalvos.Add(reuniao.Status);
            return Task.CompletedTask;
        }
    }

    private sealed class LixeiraFake(bool existe, bool falharAoMover = false) : IGravacaoLixeira
    {
        public string? CaminhoMovido { get; private set; }

        public Task<bool> ExisteAsync(string caminhoArquivo, CancellationToken cancellationToken) => Task.FromResult(existe);

        public Task MoverAsync(string caminhoArquivo, CancellationToken cancellationToken)
        {
            if (falharAoMover)
            {
                throw new IOException("A Lixeira não está disponível.");
            }

            CaminhoMovido = caminhoArquivo;
            return Task.CompletedTask;
        }
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
