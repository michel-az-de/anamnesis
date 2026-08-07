using Anamnesis.Infrastructure.Whisper;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class WhisperDockerComandoTests
{
    [Fact]
    public void DeveMontarSomenteModeloAudioESaida()
    {
        var options = new WhisperOptions(
            "C:\\Docker\\docker.exe",
            "C:\\Anamnesis\\models\\ggml-base.bin",
            "pt",
            "C:\\FFmpeg\\ffmpeg.exe",
            "ghcr.io/ggml-org/whisper.cpp@sha256:teste");

        var argumentos = WhisperDockerComando.Criar(
            options,
            "C:\\Temp\\audio\\entrada.wav",
            "C:\\Temp\\saida\\resultado");

        Assert.Equal(
            [
                "run", "--rm",
                "-v", "C:\\Anamnesis\\models\\ggml-base.bin:/models/model.bin:ro",
                "-v", "C:\\Temp\\audio\\entrada.wav:/audio/input.wav:ro",
                "-v", "C:\\Temp\\saida:/output",
                "ghcr.io/ggml-org/whisper.cpp@sha256:teste",
                "whisper-cli -m /models/model.bin -f /audio/input.wav -l pt -mc 0 -sns -np -otxt -of /output/resultado"
            ],
            argumentos);
    }
}
