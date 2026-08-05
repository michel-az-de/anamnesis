using Anamnesis.Application.Modelos;
using Anamnesis.Domain.Entidades;

namespace Anamnesis.Application.Contracts;

public interface IArquivador
{
    Task<ArtefatosReuniao> ArquivarAsync(Reuniao reuniao, CancellationToken cancellationToken);
}
