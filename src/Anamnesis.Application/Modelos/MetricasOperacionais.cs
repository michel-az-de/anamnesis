namespace Anamnesis.Application.Modelos;

public sealed record MetricasOperacionais(
    int TotalEventos,
    int Alertas,
    double DuracaoMediaMs);
