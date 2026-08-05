namespace Anamnesis.Application.Modelos;

public sealed record EventoOperacionalFiltro(
    NivelEventoOperacional? Nivel = null,
    string? Componente = null,
    string? Codigo = null,
    Guid? ReuniaoId = null,
    Guid? JobId = null,
    DateTimeOffset? InicioUtc = null,
    DateTimeOffset? FimUtc = null,
    int Limite = 500);
