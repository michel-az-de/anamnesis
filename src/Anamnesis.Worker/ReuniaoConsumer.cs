using Anamnesis.Application.Contracts;
using Anamnesis.Application.Modelos;
using Anamnesis.Application.Observabilidade;
using Anamnesis.Application.UseCases;

namespace Anamnesis.Worker;

public sealed class ReuniaoConsumer(
    IJobQueue fila,
    ProcessarReuniaoHandler processarReuniaoHandler,
    TimeProvider relogio,
    JornalOperacional? journal = null)
{
    private readonly JornalOperacional _journal = journal ?? JornalOperacional.Nulo;

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

        await _journal.RegistrarAsync(
            NivelEventoOperacional.Info,
            CodigosEventoOperacional.JobReservado,
            "Worker",
            "Job reservado para processamento.",
            job.ReuniaoId,
            job.Id,
            new MetadadosEventoOperacional(
                Operacao: "reservar_job",
                Tentativa: job.Tentativas,
                Resultado: "sucesso"),
            cancellationToken);

        try
        {
            await processarReuniaoHandler.ExecutarAsync(job.ReuniaoId, job.Id, cancellationToken);
            await fila.ConcluirAsync(job.Id, relogio.GetUtcNow(), cancellationToken);
            await _journal.RegistrarAsync(
                NivelEventoOperacional.Info,
                CodigosEventoOperacional.JobConcluido,
                "Worker",
                "Job concluído.",
                job.ReuniaoId,
                job.Id,
                new MetadadosEventoOperacional(
                    Operacao: "concluir_job",
                    Tentativa: job.Tentativas,
                    Resultado: "sucesso"),
                cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            await _journal.RegistrarFalhaAsync(
                "Worker",
                "processar_job",
                exception,
                job.ReuniaoId,
                job.Id,
                CancellationToken.None);
            await fila.LiberarAsync(job.Id, CancellationToken.None);
            throw;
        }
    }
}
