using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.Contracts;

public interface IJobQuery
{
    Task<JobResumo?> ObterMaisRecenteAsync(
        Guid reuniaoId,
        CancellationToken cancellationToken);
}

