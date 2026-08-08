using System;

namespace Anamnesis.Domain.Entidades;

public class ContaAgenda
{
    public string ContaAgendaId { get; set; } = Guid.NewGuid().ToString("N");
    public string Provider { get; set; } = string.Empty; // "Google" ou "Microsoft"
    public string Estado { get; set; } = "Desconectada"; // "Conectada", "Sincronizando", "RequerAtencao", "Desconectada"
    public string? CursorSync { get; set; }
    public string? JanelaSyncInicio { get; set; }
    public string? JanelaSyncFim { get; set; }
    public string? AtualizadoEm { get; set; }
}
