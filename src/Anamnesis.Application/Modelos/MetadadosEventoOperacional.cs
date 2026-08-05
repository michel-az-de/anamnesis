namespace Anamnesis.Application.Modelos;

public sealed record MetadadosEventoOperacional(
    string? Operacao = null,
    int? Tentativa = null,
    string? Resultado = null,
    string? MotivoCodigo = null,
    double? DuracaoMs = null);
