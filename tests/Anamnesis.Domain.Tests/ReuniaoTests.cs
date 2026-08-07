using Anamnesis.Domain.Entidades;
using Anamnesis.Domain.Tipos;
using Xunit;

namespace Anamnesis.Domain.Tests;

public sealed class ReuniaoTests
{
    [Fact]
    public void DevePrepararProcessamentoDepoisDeFinalizarGravacao()
    {
        var criadaEm = new DateTimeOffset(2026, 8, 4, 14, 0, 0, TimeSpan.Zero);
        var reuniao = new Reuniao(Guid.NewGuid(), "Planejamento", criadaEm);

        reuniao.IniciarGravacao(criadaEm);
        reuniao.FinalizarGravacao("C:\\gravacoes\\planejamento.mkv", criadaEm.AddMinutes(45));

        Assert.Equal(StatusReuniao.AguardandoProcessamento, reuniao.Status);
        Assert.Equal("C:\\gravacoes\\planejamento.mkv", reuniao.Gravacao!.CaminhoArquivo);
    }

    [Fact]
    public void NaoDevePermitirExcluirAntesDeArquivar()
    {
        var reuniao = new Reuniao(Guid.NewGuid(), "Planejamento", DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(reuniao.MarcarPendenteExclusao);
    }

    [Fact]
    public void DeveReiniciarProcessamentoAposFalhaPreservandoGravacao()
    {
        var agora = DateTimeOffset.UtcNow;
        var reuniao = new Reuniao(Guid.NewGuid(), "Planejamento", agora);
        reuniao.IniciarGravacao(agora);
        reuniao.FinalizarGravacao("C:\\gravacoes\\planejamento.mkv", agora);
        reuniao.IniciarTranscricao();
        reuniao.RegistrarTranscricao(new Transcricao("Parcial", "pt", agora));
        reuniao.RegistrarFalha("CLI indisponível");

        reuniao.ReiniciarProcessamento();

        Assert.Equal(StatusReuniao.AguardandoProcessamento, reuniao.Status);
        Assert.Equal("C:\\gravacoes\\planejamento.mkv", reuniao.Gravacao!.CaminhoArquivo);
        Assert.Null(reuniao.Transcricao);
        Assert.Null(reuniao.Ata);
        Assert.Null(reuniao.MotivoFalha);
    }

    [Fact]
    public void DeveEditarTituloETranscricaoSemAlterarEstadoOuMetadados()
    {
        var agora = new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);
        var geradaEm = agora.AddMinutes(2);
        var reuniao = Reuniao.Reconstituir(
            Guid.NewGuid(),
            "Título anterior",
            agora,
            StatusReuniao.Arquivada,
            new Gravacao("C:\\gravacoes\\reuniao.mkv", agora, agora.AddMinutes(1)),
            new Transcricao("Texto anterior", "pt-BR", geradaEm),
            new Ata("Resumo", [], [], agora.AddMinutes(3)),
            null,
            agora.AddMinutes(4));

        reuniao.EditarTitulo("  Título revisado  ");
        reuniao.EditarTranscricao("Transcrição revisada com acentuação.");

        Assert.Equal("Título revisado", reuniao.Titulo);
        Assert.Equal("Transcrição revisada com acentuação.", reuniao.Transcricao!.Texto);
        Assert.Equal("pt-BR", reuniao.Transcricao.Idioma);
        Assert.Equal(geradaEm, reuniao.Transcricao.GeradaEm);
        Assert.Equal(StatusReuniao.Arquivada, reuniao.Status);
        Assert.NotNull(reuniao.Ata);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NaoDevePersistirEdicaoVazia(string texto)
    {
        var reuniao = new Reuniao(Guid.NewGuid(), "Reunião", DateTimeOffset.UtcNow);
        reuniao.IniciarGravacao(DateTimeOffset.UtcNow);
        reuniao.FinalizarGravacao("C:\\gravacoes\\reuniao.mkv", DateTimeOffset.UtcNow);
        reuniao.IniciarTranscricao();
        reuniao.RegistrarTranscricao(new Transcricao("Original", "pt", DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(() => reuniao.EditarTitulo(texto));
        Assert.Throws<ArgumentException>(() => reuniao.EditarTranscricao(texto));
    }
}
