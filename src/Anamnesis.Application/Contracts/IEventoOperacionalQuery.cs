using Anamnesis.Application.Modelos;

namespace Anamnesis.Application.Contracts;

public interface IEventoOperacionalQuery
{
    Task<IReadOnlyList<EventoOperacional>> ListarAsync(
        EventoOperacionalFiltro filtro,
        CancellationToken cancellationToken);

    Task<MetricasOperacionais> ObterMetricasAsync(
        EventoOperacionalFiltro filtro,
        CancellationToken cancellationToken);
}
