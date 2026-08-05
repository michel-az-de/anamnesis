using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.Contracts;

public interface IEventoOperacionalSink
{
    Task RegistrarAsync(EventoOperacional evento, CancellationToken cancellationToken);

    Task RemoverAnterioresAsync(DateTimeOffset limiteUtc, CancellationToken cancellationToken);
}
