using Anamnesis.Domain.Entidades;

namespace Anamnesis.Application.Contracts;

public interface ILembreteTarefaRepository
{
    Task SalvarAsync(LembreteTarefa lembrete, CancellationToken cancellationToken);

    Task<IReadOnlyList<LembreteTarefa>> ListarPendentesAteAsync(
        DateTimeOffset limite,
        CancellationToken cancellationToken);
}
