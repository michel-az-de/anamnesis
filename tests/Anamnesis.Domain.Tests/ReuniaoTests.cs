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
}
