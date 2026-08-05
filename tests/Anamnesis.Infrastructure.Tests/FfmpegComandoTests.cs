using Anamnesis.Infrastructure.Whisper;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class FfmpegComandoTests
{
    [Fact]
    public void DeveComporConversaoParaWavPcm16Mono16KhzSemShell()
    {
        var argumentos = FfmpegComando.Criar(
            "C:\\gravacoes\\reuniao com espaço.mkv",
            "C:\\temp\\reuniao.wav");

        Assert.Equal(
            ["-y", "-i", "C:\\gravacoes\\reuniao com espaço.mkv", "-vn", "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", "C:\\temp\\reuniao.wav"],
            argumentos);
    }
}
