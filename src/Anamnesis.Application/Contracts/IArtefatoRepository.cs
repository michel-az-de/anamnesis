using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.Contracts;

public interface IArtefatoRepository
{
    Task SalvarAsync(ArtefatosReuniao artefatos, CancellationToken cancellationToken);

    Task<ArtefatosReuniao?> ObterAsync(Guid reuniaoId, CancellationToken cancellationToken);
}
