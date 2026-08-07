using Anamnesis.Domain.Entidades;

namespace Anamnesis.Application.Contracts;

public interface INotificadorLembrete
{
    Task NotificarAsync(LembreteTarefa lembrete, CancellationToken cancellationToken);
}
