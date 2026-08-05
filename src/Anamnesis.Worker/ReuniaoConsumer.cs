using Anamnesis.Application.Contracts;
using Anamnesis.Application.UseCases;

namespace Anamnesis.Worker;

public sealed class ReuniaoConsumer(
    IJobQueue fila,
    ProcessarReuniaoHandler processarReuniaoHandler,
    TimeProvider relogio)
{
    public Task RetomarAsync(CancellationToken cancellationToken) =>
        fila.LiberarReservasAtivasAsync(cancellationToken);

    public async Task<bool> ProcessarProximoAsync(CancellationToken cancellationToken)
    {
        var job = await fila.ReservarProximoAsync(relogio.GetUtcNow(), cancellationToken);
        if (job is null)
        {
            return false;
        }

        try
        {
            await processarReuniaoHandler.ExecutarAsync(job.ReuniaoId, cancellationToken);
            await fila.ConcluirAsync(job.Id, relogio.GetUtcNow(), cancellationToken);
            return true;
        }
        catch
        {
            await fila.LiberarAsync(job.Id, CancellationToken.None);
            throw;
        }
    }
}
