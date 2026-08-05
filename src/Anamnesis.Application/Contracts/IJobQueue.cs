using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.Contracts;

public interface IJobQueue
{
    Task<JobProcessamento> EnfileirarAsync(
        Guid reuniaoId,
        DateTimeOffset criadoEm,
        CancellationToken cancellationToken);

    Task<JobProcessamento?> ReservarProximoAsync(
        DateTimeOffset reservadoEm,
        CancellationToken cancellationToken);

    Task LiberarAsync(Guid jobId, CancellationToken cancellationToken);

    /// <summary>
    /// Libera todas as reservas ativas para que o Worker retome o trabalho de uma execução anterior.
    /// Só pode ser chamado por um processo que detém a exclusividade da instância do Worker
    /// (ver ADR-012): é ela que garante que nenhuma reserva liberada aqui pertence a um Worker vivo.
    /// </summary>
    Task LiberarReservasAtivasAsync(CancellationToken cancellationToken);

    Task ConcluirAsync(Guid jobId, DateTimeOffset concluidoEm, CancellationToken cancellationToken);
}
