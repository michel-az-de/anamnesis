using Anamnesis.Domain.Entidades;

namespace Anamnesis.Application.Modelos;

public sealed record AtaGerada(
    string ResumoExecutivo,
    IReadOnlyList<string> Decisoes,
    IReadOnlyList<Tarefa> Tarefas);
