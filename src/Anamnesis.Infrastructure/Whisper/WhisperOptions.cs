namespace Anamnesis.Infrastructure.Whisper;

public sealed record WhisperOptions(
    string CaminhoExecutavel,
    string CaminhoModelo,
    string Idioma,
    string CaminhoExecutavelFfmpeg = "",
    string? ImagemDocker = null);
