namespace Anamnesis.Domain.Entidades;

public sealed record Gravacao(
    string CaminhoArquivo,
    DateTimeOffset IniciadaEm,
    DateTimeOffset FinalizadaEm);
