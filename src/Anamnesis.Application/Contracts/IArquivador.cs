using Anamnesis.Domain.Entidades;

namespace Anamnesis.Application.Contracts;

public interface IArquivador
{
    Task ArquivarAsync(Reuniao reuniao, CancellationToken cancellationToken);
}
