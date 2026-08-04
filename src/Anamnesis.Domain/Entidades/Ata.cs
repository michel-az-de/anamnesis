namespace Anamnesis.Domain.Entidades;

public sealed record Ata(
    string ResumoExecutivo,
    IReadOnlyList<string> Decisoes,
    IReadOnlyList<Tarefa> Tarefas,
    DateTimeOffset GeradaEm);
