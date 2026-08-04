namespace Anamnesis.Application.Modelos;

public sealed record JobProcessamento(
    Guid Id,
    Guid ReuniaoId,
    DateTimeOffset CriadoEm,
    DateTimeOffset? ReservadoEm,
    int Tentativas);
