using Anamnesis.Infrastructure.Whisper;
using Xunit;

namespace Anamnesis.Infrastructure.Tests;

public sealed class WhisperTranscritorTests
{
    [Fact]
    public async Task DeveConverterMkvParaWavSemAlterarGravacaoOriginal()
    {
        var diretorio = Path.Combine(Path.GetTempPath(), $"anamnesis-whisper-{Guid.NewGuid():N}");
        Directory.CreateDirectory(diretorio);
        try
        {
            var gravacao = Path.Combine(diretorio, "gravacao.mkv");
            var modelo = Path.Combine(diretorio, "modelo.bin");
            var entradaWhisper = Path.Combine(diretorio, "entrada-whisper.txt");
            await File.WriteAllTextAsync(gravacao, "gravação original");
            await File.WriteAllTextAsync(modelo, "modelo");
            var ffmpeg = await CriarFfmpegFakeAsync(diretorio);
            var whisper = await CriarWhisperFakeAsync(diretorio, entradaWhisper);
            var transcritor = new WhisperTranscritor(new WhisperOptions(whisper, modelo, "pt", ffmpeg));

            var transcricao = await transcritor.TranscreverAsync(gravacao, CancellationToken.None);

            Assert.Equal("Transcrição convertida", transcricao.Texto);
            Assert.Equal("gravação original", await File.ReadAllTextAsync(gravacao));
            var caminhoRecebido = await File.ReadAllTextAsync(entradaWhisper);
            Assert.EndsWith(".wav", caminhoRecebido.Trim(), StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(caminhoRecebido.Trim()));
        }
        finally
        {
            Directory.Delete(diretorio, recursive: true);
        }
    }

    private static async Task<string> CriarFfmpegFakeAsync(string diretorio)
    {
        var caminho = Path.Combine(diretorio, "ffmpeg-fake.cmd");
        await File.WriteAllTextAsync(caminho, "@echo off\r\nset \"saida=\"\r\n:argumento\r\nif \"%~1\"==\"\" goto fim\r\nset \"saida=%~1\"\r\nshift\r\ngoto argumento\r\n:fim\r\necho WAV convertido> \"%saida%\"\r\n");
        return caminho;
    }

    private static async Task<string> CriarWhisperFakeAsync(string diretorio, string entradaWhisper)
    {
        var caminho = Path.Combine(diretorio, "whisper-fake.cmd");
        await File.WriteAllTextAsync(
            caminho,
            $"@echo off\r\nset \"entrada=\"\r\nset \"saida=\"\r\n:argumento\r\nif \"%~1\"==\"\" goto fim\r\nif \"%~1\"==\"-f\" (set \"entrada=%~2\" & shift)\r\nif \"%~1\"==\"-of\" (set \"saida=%~2\" & shift)\r\nshift\r\ngoto argumento\r\n:fim\r\necho %entrada%> \"{entradaWhisper}\"\r\necho Transcrição convertida> \"%saida%.txt\"\r\n");
        return caminho;
    }
}
