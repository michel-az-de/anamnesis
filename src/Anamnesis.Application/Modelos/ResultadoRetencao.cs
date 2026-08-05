namespace Anamnesis.Application.Modelos;

public sealed record ResultadoRetencao(bool PodeMover, string? CaminhoArquivo, string? Motivo);
