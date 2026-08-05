namespace Anamnesis.Application.Modelos;

public enum EstadoJobProcessamento
{
    Pendente,
    EmProcessamento,
    Concluido
}

public sealed record JobResumo(
    Guid Id,
    Guid ReuniaoId,
    DateTimeOffset CriadoEm,
    DateTimeOffset? ReservadoEm,
    DateTimeOffset? ConcluidoEm,
    int Tentativas,
    EstadoJobProcessamento Estado);

