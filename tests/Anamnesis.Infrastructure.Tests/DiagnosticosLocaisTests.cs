using Anamnesis.Infrastructure.Configuracao;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class DiagnosticosLocaisTests
{
    [Fact]
    public void DeveIndicarDependenciasAusentesSemExecutaLas()
    {
        var configuracao = new ConfiguracaoAnamnesis
        {
            EnderecoObs = "invalido",
            CaminhoExecutavelFfmpeg = "C:\\ausente\\ffmpeg.exe",
            CaminhoExecutavelWhisper = "C:\\ausente\\whisper-cli.exe",
            CaminhoModeloWhisper = "C:\\ausente\\modelo.bin",
            CaminhoExecutavelCli = "C:\\ausente\\cli.exe"
        };

        var diagnosticos = DiagnosticosLocais.Avaliar(configuracao);

        Assert.Equal(5, diagnosticos.Count);
        Assert.All(diagnosticos, diagnostico => Assert.False(diagnostico.Disponivel));
        Assert.Contains(diagnosticos, diagnostico => diagnostico.Nome == "OBS" && diagnostico.Mensagem == "Endereço OBS inválido.");
    }

    [Fact]
    public void DeveIndicarWhisperDockerConfigurado()
    {
        var arquivo = Path.GetTempFileName();
        try
        {
            var configuracao = new ConfiguracaoAnamnesis
            {
                EnderecoObs = "ws://127.0.0.1:4455",
                CaminhoExecutavelObs = arquivo,
                CaminhoExecutavelFfmpeg = arquivo,
                CaminhoExecutavelWhisper = arquivo,
                CaminhoModeloWhisper = arquivo,
                CaminhoExecutavelCli = arquivo,
                ImagemDockerWhisper = "ghcr.io/ggml-org/whisper.cpp@sha256:teste"
            };

            var diagnosticos = DiagnosticosLocais.Avaliar(configuracao);

            Assert.All(diagnosticos, diagnostico => Assert.True(diagnostico.Disponivel));
            Assert.Contains(diagnosticos, diagnostico =>
                diagnostico.Nome == "Whisper CLI" && diagnostico.Mensagem == "Executor Docker e imagem configurados.");
        }
        finally
        {
            File.Delete(arquivo);
        }
    }
}
