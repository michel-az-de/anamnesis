using Anamnesis.Domain.Tipos;

namespace Anamnesis.Application.Modelos;

public sealed record ReuniaoResumo(
    Guid Id,
    string Titulo,
    DateTimeOffset CriadaEm,
    StatusReuniao Status,
    DateTimeOffset? GravacaoIniciadaEm,
    DateTimeOffset? GravacaoFinalizadaEm,
    string? MotivoFalha,
    string? SecaoCorrespondente = null,
    string? TrechoCorrespondente = null);

