using Anamnesis.Infrastructure.Whisper;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class WhisperComandoTests
{
    [Fact]
    public void DeveComporArgumentosDoWhisperSemShell()
    {
        var options = new WhisperOptions("C:\\whisper\\whisper-cli.exe", "C:\\whisper\\modelo.bin", "pt");

        var argumentos = WhisperComando.Criar(options, "C:\\gravacoes\\reuniao com espaço.mkv", "C:\\temp\\resultado");

        Assert.Equal(["-m", "C:\\whisper\\modelo.bin", "-f", "C:\\gravacoes\\reuniao com espaço.mkv", "-l", "pt", "-otxt", "-of", "C:\\temp\\resultado"], argumentos);
    }

    [Fact]
    public void DeveUsarIdiomaConfigurado()
    {
        var options = new WhisperOptions("whisper-cli.exe", "modelo.bin", "en");

        var argumentos = WhisperComando.Criar(options, "audio.wav", "saida");

        Assert.Equal("en", argumentos[5]);
    }
}
