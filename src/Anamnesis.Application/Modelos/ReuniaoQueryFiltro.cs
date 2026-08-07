using Anamnesis.Domain.Tipos;

namespace Anamnesis.Application.Modelos;

public sealed record ReuniaoQueryFiltro(
    string? Texto,
    StatusReuniao? Status,
    int Limite = 100,
    DateTimeOffset? CriadaDesde = null);

