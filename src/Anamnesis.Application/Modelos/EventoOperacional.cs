namespace Anamnesis.Application.Modelos;

public sealed record EventoOperacional(
    Guid Id,
    DateTimeOffset CriadoEm,
    NivelEventoOperacional Nivel,
    string Codigo,
    string Componente,
    string Mensagem,
    Guid? ReuniaoId,
    Guid? JobId,
    MetadadosEventoOperacional Metadados);
