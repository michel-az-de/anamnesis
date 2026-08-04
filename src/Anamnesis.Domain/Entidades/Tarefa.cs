namespace Anamnesis.Domain.Entidades;

public sealed record Tarefa(string Descricao, string? Responsavel, DateOnly? Prazo);
