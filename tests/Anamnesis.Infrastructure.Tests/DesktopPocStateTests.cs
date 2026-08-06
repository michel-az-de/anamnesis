using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class DesktopPocStateTests
{
    [Fact]
    public void DeveIniciarComHistoricoProntoParaExploracao()
    {
        var state = new DesktopPocState();

        Assert.Equal(EtapaDesktopPoc.Pronto, state.Etapa);
        Assert.Equal(4, state.Reunioes.Count);
        Assert.All(state.Reunioes, reuniao => Assert.False(string.IsNullOrWhiteSpace(reuniao.Titulo)));
    }

    [Fact]
    public void DeveSimularGravacaoProcessamentoEConclusao()
    {
        var state = new DesktopPocState();

        state.IniciarGravacao();
        state.AvancarGravacao();
        state.AvancarGravacao();

        Assert.Equal(EtapaDesktopPoc.Gravando, state.Etapa);
        Assert.Equal(TimeSpan.FromSeconds(2), state.DuracaoGravacao);

        var reuniao = state.EncerrarGravacao();

        Assert.Equal(EtapaDesktopPoc.Processando, state.Etapa);
        Assert.Equal("Transcrevendo", reuniao.Status);
        Assert.Same(reuniao, state.Reunioes[0]);

        state.ConcluirProcessamento();

        Assert.Equal(EtapaDesktopPoc.Concluido, state.Etapa);
        Assert.Equal("Ata pronta", reuniao.Status);
    }

    [Fact]
    public void DevePreservarTituloInformadoNaGravacaoManual()
    {
        var state = new DesktopPocState();

        state.IniciarGravacao("  Planejamento da alpha  ");
        var reuniao = state.EncerrarGravacao();

        Assert.Equal("Planejamento da alpha", reuniao.Titulo);
    }

    [Fact]
    public void DeveUsarTituloPadraoQuandoTituloManualEstiverVazio()
    {
        var state = new DesktopPocState();

        state.IniciarGravacao("   ");
        var reuniao = state.EncerrarGravacao();

        Assert.Equal("Reunião sem título", reuniao.Titulo);
    }

    [Fact]
    public void NaoDeveAvancarCronometroForaDaGravacao()
    {
        var state = new DesktopPocState();

        state.AvancarGravacao();

        Assert.Equal(TimeSpan.Zero, state.DuracaoGravacao);
    }
}

