using Anamnesis.Application.Contracts;
using Anamnesis.Application.UseCases;

namespace Anamnesis.Worker;

public sealed class ReuniaoConsumer(
    IJobQueue fila,
    ProcessarReuniaoHandler processarReuniaoHandler,
    TimeProvider relogio)
{
    /// <summary>
    /// Libera reservas deixadas por uma execução anterior. Só é seguro em um processo que detém
    /// a exclusividade da instância do Worker (ver ADR-012): é ela que garante que nenhuma
    /// reserva liberada aqui pertence a um Worker vivo.
    /// </summary>
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
