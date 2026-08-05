namespace Anamnesis.Application.Contracts;

public interface IJobMetricasQuery
{
    Task<int> ContarPendentesAsync(CancellationToken cancellationToken);
}
