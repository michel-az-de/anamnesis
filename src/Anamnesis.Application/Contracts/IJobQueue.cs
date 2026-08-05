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
    Task LiberarReservasAtivasAsync(CancellationToken cancellationToken);
    Task ConcluirAsync(Guid jobId, DateTimeOffset concluidoEm, CancellationToken cancellationToken);
}
