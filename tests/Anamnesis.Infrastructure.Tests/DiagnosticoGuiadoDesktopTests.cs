using Anamnesis.Tray;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class DiagnosticoGuiadoDesktopTests
{
    [Fact]
    public void NaoDeveConcluirSemTranscricaoReconhecida()
    {
        var estado = new DiagnosticoGuiadoDesktop();
        estado.Iniciar(Guid.NewGuid());
        estado.MarcarProcessando();

        estado.Concluir([]);

        Assert.Equal(EstadoDiagnosticoGuiado.Falha, estado.Estado);
        Assert.Contains("nenhum texto", estado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.False(estado.TudoCerto);
    }

    [Fact]
    public void DeveConcluirComTrechoSemExporConteudoNoDiagnosticoCopiavel()
    {
        var reuniaoId = Guid.NewGuid();
        var estado = new DiagnosticoGuiadoDesktop();
        estado.Iniciar(reuniaoId);
        estado.MarcarProcessando();

        estado.Concluir(["minha fala privada foi reconhecida"]);

        Assert.True(estado.TudoCerto);
        Assert.Contains("minha fala privada", estado.TrechoReconhecido, StringComparison.Ordinal);
        Assert.DoesNotContain("minha fala privada", estado.CriarDiagnosticoCopiavel(), StringComparison.Ordinal);
        Assert.Contains(reuniaoId.ToString("N"), estado.CriarDiagnosticoCopiavel(), StringComparison.Ordinal);
    }

    [Fact]
    public void FalhaCopiavelDeveRedigirSegredoECaminhoPrivado()
    {
        var estado = new DiagnosticoGuiadoDesktop();
        estado.Iniciar(Guid.NewGuid());

        estado.Falhar(
            "Whisper",
            "Transcrevendo",
            @"token=secreto falhou em C:\\Users\\felip\\audio.wav");

        var diagnostico = estado.CriarDiagnosticoCopiavel();
        Assert.DoesNotContain("secreto", diagnostico, StringComparison.Ordinal);
        Assert.DoesNotContain("felip", diagnostico, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[REMOVIDO]", diagnostico, StringComparison.Ordinal);
        Assert.Contains("[CAMINHO_REMOVIDO]", diagnostico, StringComparison.Ordinal);
    }

    [Fact]
    public void CancelamentoDeveExplicarQueACapturaEncerradaSegueOFluxoNormal()
    {
        var estado = new DiagnosticoGuiadoDesktop();
        estado.Iniciar(Guid.NewGuid());

        estado.Cancelar();

        Assert.Equal(EstadoDiagnosticoGuiado.Cancelado, estado.Estado);
        Assert.Contains("encerrada com segurança", estado.Mensagem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("processamento normal", estado.Mensagem, StringComparison.OrdinalIgnoreCase);
    }
}
