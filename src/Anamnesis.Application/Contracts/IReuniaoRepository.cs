using Anamnesis.Domain.Entidades;

namespace Anamnesis.Application.Contracts;

public interface IReuniaoRepository
{
    Task<Reuniao?> ObterAsync(Guid reuniaoId, CancellationToken cancellationToken);
    Task SalvarAsync(Reuniao reuniao, CancellationToken cancellationToken);
}
