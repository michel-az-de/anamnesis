namespace Anamnesis.Infrastructure.Cli;

public sealed record CliAtaRunnerOptions(
    string Nome,
    string CaminhoExecutavel,
    IReadOnlyList<string> Argumentos,
    TimeSpan? Timeout = null);
