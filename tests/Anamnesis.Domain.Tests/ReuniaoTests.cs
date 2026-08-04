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
}
