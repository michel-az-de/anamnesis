namespace Anamnesis.Domain.Entidades;

public class EventoAgenda
{
    public string EventoAgendaId { get; set; } = string.Empty;
    public string ContaAgendaId { get; set; } = string.Empty;
    public string EventoExternoId { get; set; } = string.Empty;
    public string? Titulo { get; set; }
    public string Inicio { get; set; } = string.Empty; // ISO 8601 UTC
    public string Fim { get; set; } = string.Empty;    // ISO 8601 UTC
    public string? FusoOriginal { get; set; }
    public string? UrlReuniao { get; set; } // Criptografada com DPAPI
    public string? Status { get; set; } // "Confirmado", "Tentativo", "Cancelado"
    public string? AtualizadoEm { get; set; }
}
